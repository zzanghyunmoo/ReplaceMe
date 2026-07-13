using System.Text.Json;
using DevAutomation.Infrastructure.Queues;

namespace DevAutomation.Tests;

public sealed class AgentQueueMessageTests
{
    [Fact]
    public void Agent_queue_message_round_trips_as_json()
    {
        var ticketId = Guid.NewGuid();
        var json = AgentQueueMessage.Create(ticketId).ToJson();

        var parsed = AgentQueueMessage.TryParse(json, out var message);

        Assert.True(parsed);
        Assert.Equal(1, message.Version);
        Assert.Equal(ticketId, message.TicketId);
        Assert.Equal(1, message.Attempt);
        Assert.Null(message.LastFailureReason);
    }

    [Fact]
    public void Agent_queue_message_accepts_legacy_payload_without_attempt_metadata()
    {
        var ticketId = Guid.NewGuid();
        var enqueuedAt = DateTimeOffset.Parse("2026-07-13T00:00:00Z");
        var json = JsonSerializer.Serialize(new { Version = 1, TicketId = ticketId, EnqueuedAt = enqueuedAt });

        var parsed = AgentQueueMessage.TryParse(json, out var message);

        Assert.True(parsed);
        Assert.Equal(1, message.Attempt);
    }

    [Fact]
    public void Agent_queue_message_create_retry_increments_attempt_and_records_failure_metadata()
    {
        var message = new AgentQueueMessage(1, Guid.NewGuid(), DateTimeOffset.Parse("2026-07-13T00:00:00Z"), attempt: 2);
        var failedAt = DateTimeOffset.Parse("2026-07-13T00:01:00Z");

        var retry = message.CreateRetry(" transient failure ", failedAt);

        Assert.Equal(3, retry.Attempt);
        Assert.Equal("transient failure", retry.LastFailureReason);
        Assert.Equal(failedAt, retry.LastFailedAt);
        Assert.Equal(message.EnqueuedAt, retry.EnqueuedAt);
    }

    [Fact]
    public void Agent_queue_message_rejects_invalid_json()
    {
        Assert.False(AgentQueueMessage.TryParse("not-json", out _));
    }

    [Fact]
    public void Agent_queue_message_rejects_missing_ticket_id()
    {
        var json = JsonSerializer.Serialize(new { Version = 1, TicketId = Guid.Empty, EnqueuedAt = DateTimeOffset.UtcNow, Attempt = 1 });

        Assert.False(AgentQueueMessage.TryParse(json, out _));
    }

    [Fact]
    public void Agent_queue_message_rejects_invalid_attempt_metadata()
    {
        var json = JsonSerializer.Serialize(new { Version = 1, TicketId = Guid.NewGuid(), EnqueuedAt = DateTimeOffset.UtcNow, Attempt = 0 });

        Assert.False(AgentQueueMessage.TryParse(json, out _));
    }
}
