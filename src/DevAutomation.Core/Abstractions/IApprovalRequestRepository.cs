using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public interface IApprovalRequestRepository
{
    Task<ApprovalRequest> AddAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken);
    Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ApprovalRequest?> MarkTimedOutAsync(Guid id, DateTimeOffset respondedAt, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
