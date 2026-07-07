namespace DevAutomation.Core.Entities;

public sealed class ExecutionLog
{
    private ExecutionLog() { }

    public ExecutionLog(Guid ticketId, DateTimeOffset timestamp, string eventType, string content)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        Timestamp = timestamp;
        EventType = string.IsNullOrWhiteSpace(eventType) ? "message" : eventType.Trim();
        Content = content ?? string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string EventType { get; private set; } = "message";
    public string Content { get; private set; } = string.Empty;
    public Ticket? Ticket { get; private set; }
}
