using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Abstractions;

public interface ITicketNotifier
{
    Task NotifyStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken);
}
