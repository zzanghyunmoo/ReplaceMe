using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DevAutomation.Tests;

public sealed class AgentJobReplayTests
{
    [Theory]
    [InlineData(TicketStatus.Completed)]
    [InlineData(TicketStatus.Failed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task RunAsync_SkipsTerminalTicketsWithoutRunningAgent(TicketStatus terminalStatus)
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicketInStatus(terminalStatus);
        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();
        var runner = new RecordingAgentRunner();
        var job = new AgentJob(
            dbContext,
            runner,
            new NoOpTicketNotifier(),
            new NoOpIssueTrackerService(),
            new RunnableReadinessService(),
            Options.Create(new ProfileReadinessOptions()),
            new FixedClock(DateTimeOffset.Parse("2026-07-13T00:00:00Z")),
            new TicketStateMachine(),
            NullLogger<AgentJob>.Instance);

        await job.RunAsync(ticket.Id);

        var saved = await dbContext.Tickets.SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(terminalStatus, saved.Status);
        Assert.Equal(0, runner.RunCount);
    }

    [Fact]
    public async Task RunAsync_WhenQueueRetryDefersUnhandledFailure_ResetsTicketToPendingAndRethrows()
    {
        await using var dbContext = CreateDbContext();
        var ticket = Ticket.Create(
            "Retry transient failure",
            "Throw once so Kafka retry can own the retry/DLQ decision.",
            "https://example.test/repo.git",
            "main",
            DateTimeOffset.Parse("2026-07-13T00:00:00Z"));
        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();
        var runner = new ThrowingAgentRunner();
        var job = new AgentJob(
            dbContext,
            runner,
            new NoOpTicketNotifier(),
            new NoOpIssueTrackerService(),
            new RunnableReadinessService(),
            Options.Create(new ProfileReadinessOptions()),
            new FixedClock(DateTimeOffset.Parse("2026-07-13T00:00:00Z")),
            new TicketStateMachine(),
            NullLogger<AgentJob>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync(ticket.Id, deferUnhandledFailureToQueue: true));

        var saved = await dbContext.Tickets.SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal("transient agent exception", exception.Message);
        Assert.Equal(TicketStatus.Pending, saved.Status);
        Assert.Null(saved.ContainerId);
        Assert.Null(saved.FailReason);
        Assert.Equal(1, runner.RunCount);
    }

    private static DevAutomationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DevAutomationDbContext>()
            .UseInMemoryDatabase($"agent-job-replay-{Guid.NewGuid()}")
            .Options;
        return new DevAutomationDbContext(options);
    }

    private static Ticket CreateTicketInStatus(TicketStatus status)
    {
        var now = DateTimeOffset.Parse("2026-07-13T00:00:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", "main", now);
        switch (status)
        {
            case TicketStatus.Completed:
                ticket.MarkRunning(now.AddMinutes(1));
                ticket.MarkCompleted(now.AddMinutes(2), "https://github.test/pull/1");
                break;
            case TicketStatus.Failed:
                ticket.MarkFailed(now.AddMinutes(2), "Previous failure.");
                break;
            case TicketStatus.Cancelled:
                ticket.MarkCancelled(now.AddMinutes(2), "Previous cancellation.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Status must be terminal.");
        }

        return ticket;
    }

    private sealed class RecordingAgentRunner : IAgentRunner
    {
        public int RunCount { get; private set; }

        public Task<AgentRunResult> RunAsync(
            Ticket ticket,
            Func<string, CancellationToken, Task> onContainerStarted,
            Func<AgentLogEvent, CancellationToken, Task> onLog,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new AgentRunResult(true, "https://github.test/pull/1", null));
        }

        public Task StopAsync(Guid ticketId, string? containerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingAgentRunner : IAgentRunner
    {
        public int RunCount { get; private set; }

        public Task<AgentRunResult> RunAsync(
            Ticket ticket,
            Func<string, CancellationToken, Task> onContainerStarted,
            Func<AgentLogEvent, CancellationToken, Task> onLog,
            CancellationToken cancellationToken)
        {
            RunCount++;
            throw new InvalidOperationException("transient agent exception");
        }

        public Task StopAsync(Guid ticketId, string? containerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpTicketNotifier : ITicketNotifier
    {
        public Task NotifyStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpIssueTrackerService : IIssueTrackerService
    {
        public IssueTrackerProvider Provider => IssueTrackerProvider.None;

        public Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken)
            => Task.FromResult(new ExternalIssueReference(null, null, null));

        public Task NotifyCompletedAsync(Ticket ticket, string? changeRequestUrl, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyFailedAsync(Ticket ticket, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RunnableReadinessService : IProfileReadinessService
    {
        public Task<ProfileReadinessReport> EvaluateAsync(string profileName, ProfileReadinessMode mode, CancellationToken cancellationToken)
            => Task.FromResult(ProfileReadinessReport.Create(profileName, mode, DateTimeOffset.UtcNow, [], []));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
