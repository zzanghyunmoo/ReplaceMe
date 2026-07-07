using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public interface IIssueTrackerService
{
    IssueTrackerProvider Provider { get; }

    Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken);

    Task NotifyCompletedAsync(Ticket ticket, string? changeRequestUrl, CancellationToken cancellationToken);

    Task NotifyFailedAsync(Ticket ticket, string reason, CancellationToken cancellationToken);
}
