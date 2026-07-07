using System.Text.Json;

namespace DevAutomation.Infrastructure.Queues;

public sealed record AgentQueueMessage(int Version, Guid TicketId, DateTimeOffset EnqueuedAt)
{
    public static AgentQueueMessage Create(Guid ticketId) => new(1, ticketId, DateTimeOffset.UtcNow);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static bool TryParse(string? json, out AgentQueueMessage message)
    {
        message = default!;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<AgentQueueMessage>(json);
            if (parsed is null || parsed.Version != 1 || parsed.TicketId == Guid.Empty) return false;
            message = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
