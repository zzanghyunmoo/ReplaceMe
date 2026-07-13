using DevAutomation.Core.Options;
using DevAutomation.Infrastructure.Queues;

namespace DevAutomation.Tests;

public sealed class KafkaAgentWorkerRetryTests
{
    [Theory]
    [InlineData(1, 3, KafkaAgentFailureAction.Retry)]
    [InlineData(2, 3, KafkaAgentFailureAction.Retry)]
    [InlineData(3, 3, KafkaAgentFailureAction.SendToDlq)]
    [InlineData(4, 3, KafkaAgentFailureAction.SendToDlq)]
    public void Retry_policy_requeues_until_configured_max_attempts_then_sends_to_dlq(
        int currentAttempt,
        int maxAttempts,
        KafkaAgentFailureAction expectedAction)
    {
        var message = new AgentQueueMessage(1, Guid.NewGuid(), DateTimeOffset.Parse("2026-07-13T00:00:00Z"), currentAttempt);

        var decision = KafkaAgentRetryPolicy.Decide(message, maxAttempts);

        Assert.Equal(expectedAction, decision.Action);
        Assert.Equal(maxAttempts, decision.MaxAttempts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Retry_policy_treats_invalid_max_attempts_as_single_dlq_attempt(int configuredMaxAttempts)
    {
        var message = AgentQueueMessage.Create(Guid.NewGuid());

        var decision = KafkaAgentRetryPolicy.Decide(message, configuredMaxAttempts);

        Assert.True(decision.ShouldSendToDlq);
        Assert.Equal(1, decision.MaxAttempts);
    }

    [Fact]
    public void Queue_options_default_retry_and_dlq_contract_is_bounded()
    {
        var options = new QueueOptions();

        Assert.Equal(3, options.MaxAttempts);
        Assert.Equal("devautomation.agent-jobs.dlq", options.KafkaDlqTopic);
    }
}
