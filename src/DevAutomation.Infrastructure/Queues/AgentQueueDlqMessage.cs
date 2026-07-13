using System.Text.Json;
using Confluent.Kafka;

namespace DevAutomation.Infrastructure.Queues;

public sealed record AgentQueueDlqMessage(
    int Version,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    string? OriginalKey,
    string? OriginalValue,
    Guid? TicketId,
    int? Attempt,
    string FailureReason,
    DateTimeOffset FailedAt)
{
    public static AgentQueueDlqMessage FromConsumeResult(
        ConsumeResult<string, string> result,
        AgentQueueMessage? parsedMessage,
        string failureReason,
        string? sanitizedOriginalValue,
        DateTimeOffset failedAt)
        => new(
            1,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            result.Message.Key,
            sanitizedOriginalValue,
            parsedMessage?.TicketId,
            parsedMessage?.Attempt,
            failureReason,
            failedAt);

    public string ToJson() => JsonSerializer.Serialize(this);
}
