using Confluent.Kafka;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Queues;

public sealed class KafkaTicketQueue : ITicketQueue
{
    private readonly QueueOptions _queueOptions;

    public KafkaTicketQueue(IOptions<QueueOptions> queueOptions)
    {
        _queueOptions = queueOptions.Value;
    }

    public async Task EnqueueAgentJobAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _queueOptions.KafkaBootstrapServers,
            ClientId = "devautomation-api"
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();
        var message = AgentQueueMessage.Create(ticketId).ToJson();
        await producer.ProduceAsync(_queueOptions.KafkaTopic, new Message<string, string>
        {
            Key = ticketId.ToString(),
            Value = message
        }, cancellationToken);
    }
}
