using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevAutomation.Infrastructure.Queues;

public sealed record AgentQueueMessage
{
    private const int CurrentVersion = 1;

    [JsonConstructor]
    public AgentQueueMessage(
        int version,
        Guid ticketId,
        DateTimeOffset enqueuedAt,
        int attempt = 1,
        string? lastFailureReason = null,
        DateTimeOffset? lastFailedAt = null)
    {
        Version = version;
        TicketId = ticketId;
        EnqueuedAt = enqueuedAt;
        Attempt = attempt;
        LastFailureReason = NormalizeFailureReason(lastFailureReason);
        LastFailedAt = lastFailedAt;
    }

    public int Version { get; init; } = CurrentVersion;
    public Guid TicketId { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public int Attempt { get; init; } = 1;
    public string? LastFailureReason { get; init; }
    public DateTimeOffset? LastFailedAt { get; init; }

    public static AgentQueueMessage Create(Guid ticketId) => new(CurrentVersion, ticketId, DateTimeOffset.UtcNow);

    public AgentQueueMessage CreateRetry(string failureReason, DateTimeOffset failedAt)
        => this with
        {
            Attempt = Attempt + 1,
            LastFailureReason = NormalizeFailureReason(failureReason),
            LastFailedAt = failedAt
        };

    public string ToJson() => JsonSerializer.Serialize(this);

    public static bool TryParse(string? json, out AgentQueueMessage message)
    {
        message = default!;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<AgentQueueMessage>(json);
            if (parsed is null || parsed.Version != CurrentVersion || parsed.TicketId == Guid.Empty || parsed.Attempt <= 0) return false;
            message = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? NormalizeFailureReason(string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return null;
        }

        var trimmed = failureReason.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
