using Confluent.Kafka;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class KafkaReadinessCheck : IProfileReadinessCheck
{
    private readonly QueueOptions _options;

    public KafkaReadinessCheck(IOptions<QueueOptions> options)
    {
        _options = options.Value;
    }

    public string Id => "local.kafka.connectivity";

    public Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.KafkaBootstrapServers))
        {
            return Task.FromResult(ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, "Kafka bootstrap servers are not configured.", "Set Queue:KafkaBootstrapServers."));
        }

        try
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.KafkaBootstrapServers
            }).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0
                ? ProfileReadinessCheckResult.Passed(Id, "Local", ProfileReadinessSeverity.Required, "Kafka broker metadata is reachable.")
                : ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, "Kafka returned no brokers.", "Start Kafka and verify Queue:KafkaBootstrapServers."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, $"Kafka connectivity check failed: {ex.Message}", "Start Kafka and verify Queue:KafkaBootstrapServers."));
        }
    }
}
