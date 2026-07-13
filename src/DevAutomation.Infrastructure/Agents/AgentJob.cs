using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Core.Services;
using System.Diagnostics;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Agents;

public sealed class AgentJob
{
    private readonly DevAutomationDbContext _dbContext;
    private readonly IAgentRunner _agentRunner;
    private readonly ITicketNotifier _ticketNotifier;
    private readonly IIssueTrackerService _issueTrackerService;
    private readonly IProfileReadinessService _readinessService;
    private readonly ProfileReadinessOptions _profileReadinessOptions;
    private readonly IClock _clock;
    private readonly TicketStateMachine _stateMachine;
    private readonly ILogger<AgentJob> _logger;

    public AgentJob(
        DevAutomationDbContext dbContext,
        IAgentRunner agentRunner,
        ITicketNotifier ticketNotifier,
        IIssueTrackerService issueTrackerService,
        IProfileReadinessService readinessService,
        IOptions<ProfileReadinessOptions> profileReadinessOptions,
        IClock clock,
        TicketStateMachine stateMachine,
        ILogger<AgentJob> logger)
    {
        _dbContext = dbContext;
        _agentRunner = agentRunner;
        _ticketNotifier = ticketNotifier;
        _issueTrackerService = issueTrackerService;
        _readinessService = readinessService;
        _profileReadinessOptions = profileReadinessOptions.Value;
        _clock = clock;
        _stateMachine = stateMachine;
        _logger = logger;
    }

    public async Task RunAsync(Guid ticketId)
    {
        using var activity = DevAutomationTelemetry.ActivitySource.StartActivity("AgentJob.Run", ActivityKind.Internal);
        activity?.SetTag("ticket.id", ticketId);
        var startedAt = Stopwatch.GetTimestamp();
        DevAutomationTelemetry.AgentJobsStarted.Add(1);
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

        if (ticket.Status is TicketStatus.Completed or TicketStatus.Failed or TicketStatus.Cancelled)
        {
            _logger.LogInformation(
                "Skipping already terminal ticket {TicketId} with status {TicketStatus}.",
                ticket.Id,
                ticket.Status);
            activity?.SetTag("ticket.status", ticket.Status.ToString());
            return;
        }

        if (_profileReadinessOptions.IsPreRunGateEnabled)
        {
            var readiness = await _readinessService.EvaluateAsync(_profileReadinessOptions.SelectedProfile, ProfileReadinessMode.PreRunGate, cancellationToken);
            if (!readiness.IsRunnable)
            {
                ticket.MarkFailed(_clock.UtcNow, $"Readiness gate blocked: {readiness.Summary}");
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _issueTrackerService.NotifyFailedAsync(ticket, ticket.FailReason ?? "Readiness gate blocked.", cancellationToken);
                await _ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);
                DevAutomationTelemetry.AgentJobsFailed.Add(1, new KeyValuePair<string, object?>("agent.result", "readiness_blocked"));
                activity?.SetTag("ticket.status", ticket.Status.ToString());
                return;
            }
        }

        var logBuffer = new List<ExecutionLog>();

        async Task FlushLogsAsync(CancellationToken flushCancellationToken)
        {
            if (logBuffer.Count == 0)
            {
                return;
            }

            var count = logBuffer.Count;
            _dbContext.ExecutionLogs.AddRange(logBuffer);
            logBuffer.Clear();
            await _dbContext.SaveChangesAsync(flushCancellationToken);
            DevAutomationTelemetry.ExecutionLogsFlushed.Add(count);
        }

        try
        {
            _stateMachine.MarkRunning(ticket, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);

            var result = await _agentRunner.RunAsync(
                ticket,
                async (containerId, ct) =>
                {
                    ticket.AttachContainer(containerId);
                    await _dbContext.SaveChangesAsync(ct);
                },
                async (logEvent, ct) =>
                {
                    logBuffer.Add(new ExecutionLog(ticket.Id, logEvent.Timestamp, logEvent.EventType, logEvent.Content));
                    if (logBuffer.Count >= 25)
                    {
                        await FlushLogsAsync(ct);
                    }
                },
                cancellationToken);

            await FlushLogsAsync(cancellationToken);
            ticket.ClearContainer();
            if (result.Succeeded)
            {
                _stateMachine.MarkCompleted(ticket, _clock.UtcNow, result.PullRequestUrl);
                DevAutomationTelemetry.AgentJobsCompleted.Add(1, new KeyValuePair<string, object?>("agent.result", "succeeded"));
            }
            else
            {
                _stateMachine.MarkFailed(ticket, _clock.UtcNow, result.FailureReason ?? "Agent failed.");
                DevAutomationTelemetry.AgentJobsFailed.Add(1, new KeyValuePair<string, object?>("agent.result", "failed"));
            }

            activity?.SetTag("ticket.status", ticket.Status.ToString());
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (result.Succeeded)
            {
                await _issueTrackerService.NotifyCompletedAsync(ticket, result.PullRequestUrl, cancellationToken);
            }
            else
            {
                await _issueTrackerService.NotifyFailedAsync(ticket, ticket.FailReason ?? "Agent failed.", cancellationToken);
            }

            await _ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent job failed for ticket {TicketId}.", ticketId);
            await FlushLogsAsync(CancellationToken.None);
            ticket.ClearContainer();
            if (ticket.Status != TicketStatus.Cancelled)
            {
                _stateMachine.MarkFailed(ticket, _clock.UtcNow, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                DevAutomationTelemetry.AgentJobsFailed.Add(1, new KeyValuePair<string, object?>("agent.result", "exception"));
                await _dbContext.SaveChangesAsync(CancellationToken.None);
                await _issueTrackerService.NotifyFailedAsync(ticket, ticket.FailReason ?? ex.Message, CancellationToken.None);
                await _ticketNotifier.NotifyStatusChangedAsync(ticket, CancellationToken.None);
            }
        }
        finally
        {
            DevAutomationTelemetry.AgentJobDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
        }
    }
}
