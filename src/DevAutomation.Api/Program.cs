using System.Text;
using Confluent.Kafka;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Contracts;
using DevAutomation.Core.Contracts.Readiness;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Services;
using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.DependencyInjection;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Queues;
using DevAutomation.Infrastructure.Slack;
using DevAutomation.Infrastructure.Telemetry;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

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
builder.Services.AddHostedService<KafkaAgentWorker>();
builder.Services.AddEndpointsApiExplorer();

var telemetryOptions = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
if (telemetryOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(telemetryOptions.ServiceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(DevAutomationTelemetry.ActivitySourceName);

            if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                    if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders)) options.Headers = telemetryOptions.OtlpHeaders;
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(DevAutomationTelemetry.MeterName);

            if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                    if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders)) options.Headers = telemetryOptions.OtlpHeaders;
                });
            }
        });
}

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrations", true))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();

app.MapGet("/health", async (
    DevAutomationDbContext dbContext,
    IOptions<QueueOptions> queueOptions,
    CancellationToken cancellationToken) =>
{
    var checks = new Dictionary<string, string>();
    checks["db"] = await dbContext.Database.CanConnectAsync(cancellationToken) ? "ok" : "failed";

    try
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = queueOptions.Value.KafkaBootstrapServers
        }).Build();
        var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));
        checks["kafka"] = metadata.Brokers.Count > 0 ? "ok" : "failed: no brokers discovered";
    }
    catch (Exception ex)
    {
        checks["kafka"] = $"failed: {ex.Message}";
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

app.MapGet("/api/readiness/profiles/{profileName}", async (
    string profileName,
    IProfileReadinessService readinessService,
    CancellationToken cancellationToken) =>
{
    var report = await readinessService.EvaluateAsync(profileName, ProfileReadinessMode.Inspect, cancellationToken);
    return Results.Ok(ProfileReadinessReportResponse.From(report));
});

app.MapPost("/api/readiness/profiles/{profileName}/doctor", async (
    string profileName,
    IProfileReadinessService readinessService,
    CancellationToken cancellationToken) =>
{
    var report = await readinessService.EvaluateAsync(profileName, ProfileReadinessMode.Doctor, cancellationToken);
    return Results.Ok(ProfileReadinessReportResponse.From(report));
});

app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    DevAutomationDbContext dbContext,
    ITicketQueue ticketQueue,
    IIssueTrackerService issueTrackerService,
    IOptions<IssueTrackerOptions> issueTrackerOptions,
    IOptions<ProfileReadinessOptions> profileReadinessOptions,
    IProfileReadinessService readinessService,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    if (profileReadinessOptions.Value.IsPreRunGateEnabled)
    {
        var readiness = await readinessService.EvaluateAsync(profileReadinessOptions.Value.SelectedProfile, ProfileReadinessMode.PreRunGate, cancellationToken);
        if (!readiness.IsRunnable)
        {
            return Results.Problem(
                title: "Readiness gate blocked ticket creation",
                detail: readiness.Summary,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["readiness"] = ProfileReadinessReportResponse.From(readiness) });
        }
    }

    var ticket = Ticket.Create(request.Title, request.Description, request.RepoUrl, request.BaseBranch, clock.UtcNow);
    var issueProvider = issueTrackerOptions.Value.Provider;
    var hasExternalIssueReference = !string.IsNullOrWhiteSpace(request.ExternalIssueId)
        || !string.IsNullOrWhiteSpace(request.ExternalIssueKey)
        || !string.IsNullOrWhiteSpace(request.ExternalIssueUrl);

    if ((request.CreateExternalIssue || hasExternalIssueReference) && issueProvider == IssueTrackerProvider.None)
    {
        return Results.BadRequest("IssueTracker:Provider must be Jira or Linear when creating or linking an external issue.");
    }

    if (hasExternalIssueReference)
    {
        ticket.AttachIssueTracker(issueProvider, request.ExternalIssueId, request.ExternalIssueKey, request.ExternalIssueUrl);
    }

    await dbContext.Tickets.AddAsync(ticket, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    if (request.CreateExternalIssue)
    {
        try
        {
            var issue = await issueTrackerService.CreateIssueAsync(ticket, cancellationToken);
            ticket.AttachIssueTracker(issueProvider, issue.Id, issue.Key, issue.Url);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ticket.MarkFailed(clock.UtcNow, $"External issue creation failed: {ex.Message}");
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Problem(title: "External issue creation failed", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    DevAutomationTelemetry.TicketsCreated.Add(1, new KeyValuePair<string, object?>("issue.provider", issueProvider.ToString()), new KeyValuePair<string, object?>("queue.provider", "Kafka"));
    await ticketQueue.EnqueueAgentJobAsync(ticket.Id, cancellationToken);
    return Results.Created($"/api/tickets/{ticket.Id}", TicketResponse.From(ticket));
});

app.MapGet("/api/tickets/{id:guid}", async (Guid id, DevAutomationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var ticket = await dbContext.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    return ticket is null ? Results.NotFound() : Results.Ok(TicketResponse.From(ticket));
});

app.MapPost("/api/tickets/{id:guid}/documents", async (
    Guid id,
    DevAutomationDbContext dbContext,
    IDocumentToolService documentToolService,
    CancellationToken cancellationToken) =>
{
    var ticket = await dbContext.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (ticket is null) return Results.NotFound();

    try
    {
        var document = await documentToolService.CreateTicketDocumentAsync(ticket, cancellationToken);
        return Results.Ok(document);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
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

app.MapPost("/api/approvals/{id:guid}/approve", async (
    Guid id,
    DevAutomationDbContext dbContext,
    IApprovalNotifier notifier,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (approval is null) return Results.NotFound();
    if (approval.Status != ApprovalStatus.Pending) return Results.Conflict(ApprovalRequestResponse.From(approval));

    approval.Approve("api", clock.UtcNow);
    await dbContext.SaveChangesAsync(cancellationToken);
    await notifier.UpdateApprovalResultAsync(approval, cancellationToken);
    return Results.Ok(ApprovalRequestResponse.From(approval));
});

app.MapPost("/api/approvals/{id:guid}/reject", async (
    Guid id,
    RejectApprovalRequest request,
    DevAutomationDbContext dbContext,
    IApprovalNotifier notifier,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (approval is null) return Results.NotFound();
    if (approval.Status != ApprovalStatus.Pending) return Results.Conflict(ApprovalRequestResponse.From(approval));

    approval.Reject("api", clock.UtcNow, string.IsNullOrWhiteSpace(request.Reason) ? "Rejected via API." : request.Reason.Trim());
    await dbContext.SaveChangesAsync(cancellationToken);
    await notifier.UpdateApprovalResultAsync(approval, cancellationToken);
    return Results.Ok(ApprovalRequestResponse.From(approval));
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

public partial class Program { }
