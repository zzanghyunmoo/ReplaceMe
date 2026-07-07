using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevAutomation.Infrastructure.Agents;

public sealed class AgentJob
{
    private readonly DevAutomationDbContext _dbContext;
    private readonly IAgentRunner _agentRunner;
    private readonly ITicketNotifier _ticketNotifier;
    private readonly IClock _clock;
    private readonly TicketStateMachine _stateMachine;
    private readonly ILogger<AgentJob> _logger;

    public AgentJob(
        DevAutomationDbContext dbContext,
        IAgentRunner agentRunner,
        ITicketNotifier ticketNotifier,
        IClock clock,
        TicketStateMachine stateMachine,
        ILogger<AgentJob> logger)
    {
        _dbContext = dbContext;
        _agentRunner = agentRunner;
        _ticketNotifier = ticketNotifier;
        _clock = clock;
        _stateMachine = stateMachine;
        _logger = logger;
    }

    public async Task RunAsync(Guid ticketId)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

        if (ticket.Status == TicketStatus.Cancelled)
        {
            return;
        }

        var logBuffer = new List<ExecutionLog>();

        async Task FlushLogsAsync(CancellationToken flushCancellationToken)
        {
            if (logBuffer.Count == 0)
            {
                return;
            }

            _dbContext.ExecutionLogs.AddRange(logBuffer);
            logBuffer.Clear();
            await _dbContext.SaveChangesAsync(flushCancellationToken);
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
            }
            else
            {
                _stateMachine.MarkFailed(ticket, _clock.UtcNow, result.FailureReason ?? "Agent failed.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
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
                await _dbContext.SaveChangesAsync(CancellationToken.None);
                await _ticketNotifier.NotifyStatusChangedAsync(ticket, CancellationToken.None);
            }
        }
    }
}
