using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public sealed record AgentLogEvent(DateTimeOffset Timestamp, string EventType, string Content);
public sealed record AgentRunResult(bool Succeeded, string? PullRequestUrl, string? FailureReason);

public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        Ticket ticket,
        Func<string, CancellationToken, Task> onContainerStarted,
        Func<AgentLogEvent, CancellationToken, Task> onLog,
        CancellationToken cancellationToken);

    Task StopAsync(Guid ticketId, string? containerId, CancellationToken cancellationToken);
}
