using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
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
    private readonly SecretRedactor _secretRedactor;
    private readonly ILogger<KafkaAgentWorker> _logger;

    public KafkaAgentWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<QueueOptions> queueOptions,
        IOptions<AgentOptions> agentOptions,
        SecretRedactor secretRedactor,
        ILogger<KafkaAgentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queueOptions = queueOptions.Value;
        _agentOptions = agentOptions.Value;
        _secretRedactor = secretRedactor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTopicsExistAsync(stoppingToken);
        var workerCount = Math.Max(1, _agentOptions.MaxConcurrentAgents);
        var workers = Enumerable.Range(0, workerCount).Select(index => ConsumeAsync(index, stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task ConsumeAsync(int workerIndex, CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _queueOptions.KafkaBootstrapServers,
            GroupId = _queueOptions.KafkaConsumerGroupId,
            ClientId = $"devautomation-agent-worker-{workerIndex}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _queueOptions.KafkaBootstrapServers,
            ClientId = $"devautomation-agent-worker-retry-{workerIndex}"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
                consumer.Subscribe(_queueOptions.KafkaTopic);
                _logger.LogInformation("Kafka agent worker {WorkerIndex} consuming {Topic} with max attempts {MaxAttempts} and DLQ {DlqTopic}.", workerIndex, _queueOptions.KafkaTopic, KafkaAgentRetryPolicy.NormalizeMaxAttempts(_queueOptions.MaxAttempts), _queueOptions.KafkaDlqTopic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(stoppingToken);
                    if (!AgentQueueMessage.TryParse(result.Message.Value, out var message))
                    {
                        await PublishInvalidMessageToDlqAsync(producer, result, stoppingToken);
                        consumer.Commit(result);
                        continue;
                    }

                    try
                    {
                        await RunAgentJobAsync(message.TicketId, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await HandleProcessingFailureAsync(producer, result, message, ex, stoppingToken);
                        consumer.Commit(result);
                    }
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

    private async Task PublishInvalidMessageToDlqAsync(
        IProducer<string, string> producer,
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        const string failureReason = "Invalid agent queue message JSON or missing required ticket metadata.";
        var failedAt = DateTimeOffset.UtcNow;
        var dlqMessage = AgentQueueDlqMessage.FromConsumeResult(
            result,
            parsedMessage: null,
            failureReason,
            SanitizePayload(result.Message.Value),
            failedAt);

        await producer.ProduceAsync(_queueOptions.KafkaDlqTopic, new Message<string, string>
        {
            Key = result.Message.Key,
            Value = dlqMessage.ToJson()
        }, cancellationToken);

        DevAutomationTelemetry.KafkaAgentJobsDlqPublished.Add(1, new KeyValuePair<string, object?>("dlq.reason", "invalid_message"));
        _logger.LogWarning(
            "Published invalid Kafka queue message at {TopicPartitionOffset} to DLQ topic {DlqTopic}.",
            result.TopicPartitionOffset,
            _queueOptions.KafkaDlqTopic);
    }

    private async Task HandleProcessingFailureAsync(
        IProducer<string, string> producer,
        ConsumeResult<string, string> result,
        AgentQueueMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAt = DateTimeOffset.UtcNow;
        var failureReason = SanitizeFailureReason(exception.Message);
        var decision = KafkaAgentRetryPolicy.Decide(message, _queueOptions.MaxAttempts);

        if (decision.ShouldRetry)
        {
            var retryMessage = message.CreateRetry(failureReason, failedAt);
            await producer.ProduceAsync(_queueOptions.KafkaTopic, new Message<string, string>
            {
                Key = message.TicketId.ToString(),
                Value = retryMessage.ToJson()
            }, cancellationToken);

            DevAutomationTelemetry.KafkaAgentJobsRetried.Add(1);
            _logger.LogWarning(
                exception,
                "Agent job for ticket {TicketId} failed on attempt {Attempt}/{MaxAttempts}; requeued as attempt {NextAttempt}.",
                message.TicketId,
                message.Attempt,
                decision.MaxAttempts,
                retryMessage.Attempt);
            return;
        }

        var dlqMessage = AgentQueueDlqMessage.FromConsumeResult(
            result,
            message,
            failureReason,
            SanitizePayload(result.Message.Value),
            failedAt);

        await producer.ProduceAsync(_queueOptions.KafkaDlqTopic, new Message<string, string>
        {
            Key = message.TicketId.ToString(),
            Value = dlqMessage.ToJson()
        }, cancellationToken);

        await MarkTicketFailedAfterDlqAsync(message.TicketId, decision.MaxAttempts, failureReason, cancellationToken);
        DevAutomationTelemetry.KafkaAgentJobsDlqPublished.Add(1, new KeyValuePair<string, object?>("dlq.reason", "attempts_exhausted"));
        _logger.LogError(
            exception,
            "Agent job for ticket {TicketId} exhausted {MaxAttempts} attempts and was published to DLQ topic {DlqTopic}.",
            message.TicketId,
            decision.MaxAttempts,
            _queueOptions.KafkaDlqTopic);
    }

    private async Task EnsureTopicsExistAsync(CancellationToken stoppingToken)
    {
        var topics = new[] { _queueOptions.KafkaTopic, _queueOptions.KafkaDlqTopic }
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = _queueOptions.KafkaBootstrapServers
                }).Build();

                await adminClient.CreateTopicsAsync(topics.Select(topic => new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }));

                _logger.LogInformation("Kafka topics {Topics} created or verified.", string.Join(", ", topics));
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(result => result.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
            {
                _logger.LogInformation("Kafka topics {Topics} created or already exist.", string.Join(", ", topics));
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka topics {Topics} are not ready; retrying after delay.", string.Join(", ", topics));
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunAgentJobAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<AgentJob>();
        await job.RunAsync(ticketId, AgentJobFailureHandling.ResetToPendingAndRethrow);
    }

    private async Task MarkTicketFailedAfterDlqAsync(
        Guid ticketId,
        int maxAttempts,
        string failureReason,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            _logger.LogWarning("Ticket {TicketId} was not found while marking exhausted Kafka job failure.", ticketId);
            return;
        }

        if (ticket.Status is TicketStatus.Completed or TicketStatus.Failed or TicketStatus.Cancelled)
        {
            _logger.LogInformation(
                "Ticket {TicketId} already has terminal status {TicketStatus}; DLQ exhaustion will not change it.",
                ticket.Id,
                ticket.Status);
            return;
        }

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var terminalReason = $"Agent job exhausted {maxAttempts} Kafka attempt(s) and was published to DLQ: {failureReason}";
        ticket.MarkFailed(clock.UtcNow, terminalReason);
        await dbContext.SaveChangesAsync(cancellationToken);

        var issueTrackerService = scope.ServiceProvider.GetRequiredService<IIssueTrackerService>();
        var ticketNotifier = scope.ServiceProvider.GetRequiredService<ITicketNotifier>();
        await NotifyTerminalFailureAsync(issueTrackerService, ticketNotifier, ticket, terminalReason, cancellationToken);
    }

    private async Task NotifyTerminalFailureAsync(
        IIssueTrackerService issueTrackerService,
        ITicketNotifier ticketNotifier,
        Ticket ticket,
        string terminalReason,
        CancellationToken cancellationToken)
    {
        try
        {
            await issueTrackerService.NotifyFailedAsync(ticket, terminalReason, cancellationToken);
            await ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ticket {TicketId} was marked failed after DLQ publish but failure notification did not complete.", ticket.Id);
        }
    }

    private string SanitizeFailureReason(string? failureReason)
        => Truncate(_secretRedactor.Redact(string.IsNullOrWhiteSpace(failureReason) ? "Unknown agent job failure." : failureReason), 500);

    private string? SanitizePayload(string? payload)
        => payload is null ? null : Truncate(_secretRedactor.Redact(payload), 2_000);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
