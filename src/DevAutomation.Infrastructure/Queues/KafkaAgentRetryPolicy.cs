namespace DevAutomation.Infrastructure.Queues;

public enum KafkaAgentFailureAction
{
    Retry,
    SendToDlq
}

public sealed record KafkaAgentFailureDecision(KafkaAgentFailureAction Action, int MaxAttempts)
{
    public bool ShouldRetry => Action == KafkaAgentFailureAction.Retry;
    public bool ShouldSendToDlq => Action == KafkaAgentFailureAction.SendToDlq;
}

public static class KafkaAgentRetryPolicy
{
    public static KafkaAgentFailureDecision Decide(AgentQueueMessage message, int configuredMaxAttempts)
    {
        var maxAttempts = NormalizeMaxAttempts(configuredMaxAttempts);
        var action = message.Attempt < maxAttempts
            ? KafkaAgentFailureAction.Retry
            : KafkaAgentFailureAction.SendToDlq;

        return new KafkaAgentFailureDecision(action, maxAttempts);
    }

    public static int NormalizeMaxAttempts(int configuredMaxAttempts) => Math.Max(1, configuredMaxAttempts);
}
