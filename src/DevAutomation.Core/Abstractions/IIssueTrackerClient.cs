using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public sealed record ExternalIssueReference(string? Id, string? Key, string? Url);

public interface IIssueTrackerClient
{
    IssueTrackerProvider Provider { get; }

    Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken);

    Task AddCommentAsync(Ticket ticket, string message, CancellationToken cancellationToken);
}
