using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevAutomation.Tests;

public sealed class RunPassportEndpointTests
{
    [Fact]
    public async Task Existing_ticket_returns_exact_v1_wire_shape_without_mutation()
    {
        await using var factory = new RunPassportApiFactory();
        using var client = factory.CreateClient();
        var ticket = await factory.SeedTicketAsync();

        using var response = await client.GetAsync($"/api/tickets/{ticket.Id}/run-passport");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        RunPassportV1JsonAssert.IsPendingLinearPassport(root, ticket.Id, ticket.CreatedAt);

        var snapshot = await factory.GetSnapshotAsync(ticket.Id);
        Assert.Equal(TicketStatus.Pending, snapshot.Status);
        Assert.Equal(1, snapshot.ExecutionLogCount);
        Assert.Equal(0, factory.Queue.EnqueueCount);
    }

    [Fact]
    public async Task Unknown_ticket_returns_not_found()
    {
        await using var factory = new RunPassportApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}/run-passport");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Queue.EnqueueCount);
    }

    [Fact]
    public async Task Configured_jira_host_survives_endpoint_projection()
    {
        await using var factory = new RunPassportApiFactory();
        using var client = factory.CreateClient();
        var ticket = await factory.SeedTicketAsync(x =>
            x.AttachIssueTracker(
                IssueTrackerProvider.Jira,
                "10001",
                "DEV-1",
                "https://jira.example.com/browse/DEV-1"));

        using var response = await client.GetAsync($"/api/tickets/{ticket.Id}/run-passport");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Jira", payload.GetProperty("issueTracker").GetString());
        Assert.Equal("https://jira.example.com/browse/DEV-1", payload.GetProperty("externalIssueUrl").GetString());
    }

    private sealed class RunPassportApiFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"run-passport-endpoint-{Guid.NewGuid()}";

        public RecordingTicketQueue Queue { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrations"] = "false",
                    ["Telemetry:Enabled"] = "false",
                    ["Notifier:Provider"] = "None",
                    ["IssueTracker:Provider"] = "None",
                    ["DocumentTool:Provider"] = "None",
                    ["ProfileReadiness:SelectedProfile"] = string.Empty,
                    ["Jira:BaseUrl"] = "https://jira.example.com"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DevAutomationDbContext>>();
                services.RemoveAll<DevAutomationDbContext>();
                services.AddDbContext<DevAutomationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));

                services.RemoveAll<ITicketQueue>();
                services.AddSingleton<ITicketQueue>(Queue);
            });
        }

        public async Task<Ticket> SeedTicketAsync(Action<Ticket>? configure = null)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
            var ticket = Ticket.Create(
                "Build feature",
                "Do the work",
                "https://github.com/example/repo.git",
                "main",
                createdAt);
            ticket.AttachIssueTracker(
                IssueTrackerProvider.Linear,
                "issue-id",
                "ZZA-56",
                "https://linear.app/example/issue/ZZA-56");
            configure?.Invoke(ticket);
            dbContext.Tickets.Add(ticket);
            dbContext.ExecutionLogs.Add(new ExecutionLog(ticket.Id, createdAt, "seed", "Existing log"));
            await dbContext.SaveChangesAsync();
            return ticket;
        }

        public async Task<(TicketStatus Status, int ExecutionLogCount)> GetSnapshotAsync(Guid ticketId)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
            var status = await dbContext.Tickets.AsNoTracking()
                .Where(x => x.Id == ticketId)
                .Select(x => x.Status)
                .SingleAsync();
            var executionLogCount = await dbContext.ExecutionLogs.AsNoTracking()
                .CountAsync(x => x.TicketId == ticketId);
            return (status, executionLogCount);
        }
    }

    private sealed class RecordingTicketQueue : ITicketQueue
    {
        private int _enqueueCount;

        public int EnqueueCount => Volatile.Read(ref _enqueueCount);

        public Task EnqueueAgentJobAsync(Guid ticketId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _enqueueCount);
            return Task.CompletedTask;
        }
    }
}
