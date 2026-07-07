using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Services;

public sealed class TicketStateMachine
{
    public void MarkRunning(Ticket ticket, DateTimeOffset now) => ticket.MarkRunning(now);
    public void MarkWaitingApproval(Ticket ticket) => ticket.MarkWaitingApproval();
    public void MarkCompleted(Ticket ticket, DateTimeOffset now, string? prUrl) => ticket.MarkCompleted(now, prUrl);
    public void MarkFailed(Ticket ticket, DateTimeOffset now, string reason) => ticket.MarkFailed(now, reason);
    public void MarkCancelled(Ticket ticket, DateTimeOffset now, string? reason = null) => ticket.MarkCancelled(now, reason);
}
