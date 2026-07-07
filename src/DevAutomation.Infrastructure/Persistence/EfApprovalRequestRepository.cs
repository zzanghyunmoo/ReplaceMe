using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevAutomation.Infrastructure.Persistence;

public sealed class EfApprovalRequestRepository : IApprovalRequestRepository
{
    private readonly DevAutomationDbContext _dbContext;

    public EfApprovalRequestRepository(DevAutomationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApprovalRequest> AddAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
    {
        await _dbContext.ApprovalRequests.AddAsync(approvalRequest, cancellationToken);
        return approvalRequest;
    }

    public Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<ApprovalRequest?> MarkTimedOutAsync(Guid id, DateTimeOffset respondedAt, CancellationToken cancellationToken)
    {
        await _dbContext.ApprovalRequests
            .Where(x => x.Id == id && x.Status == ApprovalStatus.Pending)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.Status, ApprovalStatus.TimedOut)
                .SetProperty(x => x.RespondedAt, respondedAt)
                .SetProperty(x => x.ResponseReason, "Approval timed out."), cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
