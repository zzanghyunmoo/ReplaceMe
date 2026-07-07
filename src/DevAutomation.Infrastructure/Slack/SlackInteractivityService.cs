using System.Text.Json;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Abstractions;
using DevAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevAutomation.Infrastructure.Slack;

public sealed class SlackInteractivityService
{
    private readonly DevAutomationDbContext _dbContext;
    private readonly IApprovalNotifier _notifier;

    public SlackInteractivityService(DevAutomationDbContext dbContext, IApprovalNotifier notifier)
    {
        _dbContext = dbContext;
        _notifier = notifier;
    }

    public async Task HandlePayloadAsync(string payloadJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var userId = root.GetProperty("user").GetProperty("id").GetString() ?? "unknown";
        var action = root.GetProperty("actions")[0];
        var actionId = action.GetProperty("action_id").GetString();
        var value = action.GetProperty("value").GetString();

        if (!Guid.TryParse(value, out var approvalId))
        {
            throw new InvalidOperationException("Invalid approval request id in Slack payload.");
        }

        var targetStatus = actionId == SlackApprovalActionIds.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        var responseReason = targetStatus == ApprovalStatus.Rejected ? "Rejected in Slack." : null;
        var now = DateTimeOffset.UtcNow;

        var affected = await _dbContext.ApprovalRequests
            .Where(x => x.Id == approvalId && x.Status == ApprovalStatus.Pending)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.Status, targetStatus)
                .SetProperty(x => x.ResponderSlackId, userId)
                .SetProperty(x => x.RespondedAt, now)
                .SetProperty(x => x.ResponseReason, responseReason), cancellationToken);

        var approval = await _dbContext.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
            ?? throw new InvalidOperationException($"Approval request {approvalId} was not found.");

        if (affected == 1)
        {
            await _notifier.UpdateApprovalResultAsync(approval, cancellationToken);
        }
    }
}
