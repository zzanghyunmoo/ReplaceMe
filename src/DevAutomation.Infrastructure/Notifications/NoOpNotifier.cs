using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;

namespace DevAutomation.Infrastructure.Notifications;

public sealed class NoOpNotifier : ITicketNotifier, IApprovalNotifier
{
    public Task NotifyStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<SlackMessageRef> SendApprovalRequestAsync(ApprovalRequest approvalRequest, ApprovalNotification notification, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SlackMessageRef("none", "not-configured"));
    }

    public Task UpdateApprovalResultAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken) => Task.CompletedTask;
}
