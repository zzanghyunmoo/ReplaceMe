namespace DevAutomation.Core.Options;

public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    public string KafkaBootstrapServers { get; set; } = "localhost:9092";
    public string KafkaTopic { get; set; } = "devautomation.agent-jobs";
    public string KafkaDlqTopic { get; set; } = "devautomation.agent-jobs.dlq";
    public string KafkaConsumerGroupId { get; set; } = "devautomation-api";
    public int MaxAttempts { get; set; } = 3;
}
