namespace DevAutomation.Core.Abstractions;

public interface ITicketQueue
{
    Task EnqueueAgentJobAsync(Guid ticketId, CancellationToken cancellationToken);
}
