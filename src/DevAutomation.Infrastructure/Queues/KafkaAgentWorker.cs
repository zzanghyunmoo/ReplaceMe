using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DevAutomation.Core.Options;
using DevAutomation.Infrastructure.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Queues;

public sealed class KafkaAgentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QueueOptions _queueOptions;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<KafkaAgentWorker> _logger;

    public KafkaAgentWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<QueueOptions> queueOptions,
        IOptions<AgentOptions> agentOptions,
        ILogger<KafkaAgentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queueOptions = queueOptions.Value;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTopicExistsAsync(stoppingToken);
        var workerCount = Math.Max(1, _agentOptions.MaxConcurrentAgents);
        var workers = Enumerable.Range(0, workerCount).Select(index => ConsumeAsync(index, stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task ConsumeAsync(int workerIndex, CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _queueOptions.KafkaBootstrapServers,
            GroupId = _queueOptions.KafkaConsumerGroupId,
            ClientId = $"devautomation-agent-worker-{workerIndex}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(config).Build();
                consumer.Subscribe(_queueOptions.KafkaTopic);
                _logger.LogInformation("Kafka agent worker {WorkerIndex} consuming {Topic}.", workerIndex, _queueOptions.KafkaTopic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(stoppingToken);
                    if (!AgentQueueMessage.TryParse(result.Message.Value, out var message))
                    {
                        _logger.LogWarning("Discarding invalid Kafka queue message at {TopicPartitionOffset}.", result.TopicPartitionOffset);
                        consumer.Commit(result);
                        continue;
                    }

                    await RunAgentJobAsync(message.TicketId, stoppingToken);
                    consumer.Commit(result);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kafka agent worker {WorkerIndex} failed; reconnecting after delay.", workerIndex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task EnsureTopicExistsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = _queueOptions.KafkaBootstrapServers
                }).Build();

                await adminClient.CreateTopicsAsync([
                    new TopicSpecification
                    {
                        Name = _queueOptions.KafkaTopic,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                ]);
                _logger.LogInformation("Kafka topic {Topic} created or verified.", _queueOptions.KafkaTopic);
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                _logger.LogInformation("Kafka topic {Topic} already exists.", _queueOptions.KafkaTopic);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka topic {Topic} is not ready; retrying after delay.", _queueOptions.KafkaTopic);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunAgentJobAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<AgentJob>();
        await job.RunAsync(ticketId);
    }
}
