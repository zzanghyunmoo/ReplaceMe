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
    }

    [Fact]
    public void Agent_queue_message_rejects_invalid_json()
    {
        Assert.False(AgentQueueMessage.TryParse("not-json", out _));
    }
}
