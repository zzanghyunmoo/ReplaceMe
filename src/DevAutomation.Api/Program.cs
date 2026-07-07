using System.Text;
using DevAutomation.Core.Contracts;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.DependencyInjection;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Slack;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.States;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DEVAUTOMATION_");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/devautomation-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDevAutomationCore(builder.Configuration);
builder.Services.AddDevAutomationInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=postgres;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation";

builder.Services.AddHangfire(configuration => configuration
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgresConnectionString)));

var agentOptions = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Max(1, agentOptions.MaxConcurrentAgents);
    options.Queues = ["agents", "default"];
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrations", true))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseHangfireDashboard("/hangfire");

app.MapGet("/health", async (
    DevAutomationDbContext dbContext,
    IOptions<RedisOptions> redisOptions,
    CancellationToken cancellationToken) =>
{
    var checks = new Dictionary<string, string>();
    checks["db"] = await dbContext.Database.CanConnectAsync(cancellationToken) ? "ok" : "failed";

    try
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(redisOptions.Value.ConnectionString);
        await redis.GetDatabase().PingAsync();
        checks["redis"] = "ok";
    }
    catch (Exception ex)
    {
        checks["redis"] = $"failed: {ex.Message}";
    }

    try
    {
        using var docker = new Docker.DotNet.DockerClientConfiguration().CreateClient();
        await docker.System.PingAsync(cancellationToken);
        checks["docker"] = "ok";
    }
    catch (Exception ex)
    {
        checks["docker"] = $"failed: {ex.Message}";
    }

    return checks.Values.All(x => x == "ok") ? Results.Ok(checks) : Results.Problem(title: "Unhealthy", extensions: new Dictionary<string, object?> { ["checks"] = checks });
});

app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    DevAutomationDbContext dbContext,
    IBackgroundJobClient jobs,
    DevAutomation.Core.Abstractions.IClock clock,
    CancellationToken cancellationToken) =>
{
    var ticket = Ticket.Create(request.Title, request.Description, request.RepoUrl, request.BaseBranch, clock.UtcNow);
    await dbContext.Tickets.AddAsync(ticket, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    jobs.Create<AgentJob>(job => job.RunAsync(ticket.Id), new EnqueuedState("agents"));
    return Results.Created($"/api/tickets/{ticket.Id}", TicketResponse.From(ticket));
});

app.MapGet("/api/tickets/{id:guid}", async (Guid id, DevAutomationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var ticket = await dbContext.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    return ticket is null ? Results.NotFound() : Results.Ok(TicketResponse.From(ticket));
});

app.MapGet("/api/tickets", async (
    TicketStatus? status,
    int? page,
    int? pageSize,
    DevAutomationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.Tickets.AsNoTracking().AsQueryable();
    if (status.HasValue)
    {
        query = query.Where(x => x.Status == status.Value);
    }

    var take = Math.Clamp(pageSize ?? 20, 1, 100);
    var skip = Math.Max(0, (page ?? 1) - 1) * take;
    var items = await query.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).Select(x => TicketResponse.From(x)).ToListAsync(cancellationToken);
    return Results.Ok(items);
});

app.MapPost("/api/tickets/{id:guid}/cancel", async (
    Guid id,
    DevAutomationDbContext dbContext,
    DevAutomation.Core.Abstractions.IAgentRunner agentRunner,
    TicketStateMachine stateMachine,
    DevAutomation.Core.Abstractions.IClock clock,
    CancellationToken cancellationToken) =>
{
    var ticket = await dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (ticket is null) return Results.NotFound();

    stateMachine.MarkCancelled(ticket, clock.UtcNow, "Cancelled by API request.");
    await dbContext.SaveChangesAsync(cancellationToken);
    await agentRunner.StopAsync(ticket.Id, ticket.ContainerId, cancellationToken);
    return Results.Ok(TicketResponse.From(ticket));
});

app.MapGet("/api/tickets/{id:guid}/logs", async (
    Guid id,
    int? page,
    int? pageSize,
    DevAutomationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var exists = await dbContext.Tickets.AnyAsync(x => x.Id == id, cancellationToken);
    if (!exists) return Results.NotFound();

    var take = Math.Clamp(pageSize ?? 100, 1, 500);
    var skip = Math.Max(0, (page ?? 1) - 1) * take;
    var logs = await dbContext.ExecutionLogs.AsNoTracking()
        .Where(x => x.TicketId == id)
        .OrderBy(x => x.Timestamp)
        .Skip(skip)
        .Take(take)
        .Select(x => ExecutionLogResponse.From(x))
        .ToListAsync(cancellationToken);
    return Results.Ok(logs);
});

app.MapGet("/api/approvals", async (
    ApprovalStatus? status,
    int? page,
    int? pageSize,
    DevAutomationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.ApprovalRequests.AsNoTracking().AsQueryable();
    if (status.HasValue)
    {
        query = query.Where(x => x.Status == status.Value);
    }

    var take = Math.Clamp(pageSize ?? 50, 1, 200);
    var skip = Math.Max(0, (page ?? 1) - 1) * take;
    var items = await query.OrderByDescending(x => x.RequestedAt).Skip(skip).Take(take).Select(x => ApprovalRequestResponse.From(x)).ToListAsync(cancellationToken);
    return Results.Ok(items);
});

app.MapPost("/api/slack/interactivity", async (
    HttpRequest request,
    ISlackSignatureVerifier verifier,
    SlackInteractivityService interactivityService,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);
    var timestamp = request.Headers["X-Slack-Request-Timestamp"].ToString();
    var signature = request.Headers["X-Slack-Signature"].ToString();

    if (!verifier.Verify(timestamp, body, signature))
    {
        return Results.Unauthorized();
    }

    var parsed = QueryHelpers.ParseQuery(body);
    if (!parsed.TryGetValue("payload", out var payload))
    {
        return Results.BadRequest("Missing Slack payload.");
    }

    await interactivityService.HandlePayloadAsync(payload.ToString(), cancellationToken);
    return Results.Ok();
});

app.Run();
