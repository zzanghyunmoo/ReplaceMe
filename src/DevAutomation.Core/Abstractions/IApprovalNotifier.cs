using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public sealed record ApprovalNotification(Guid TicketId, string TicketTitle, string ToolName, string Summary, string InputJson);
public sealed record SlackMessageRef(string ChannelId, string MessageTs);

public interface IApprovalNotifier
{
    Task<SlackMessageRef> SendApprovalRequestAsync(ApprovalRequest approvalRequest, ApprovalNotification notification, CancellationToken cancellationToken);
    Task UpdateApprovalResultAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken);
}
