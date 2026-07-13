using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Options;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.DependencyInjection;
using DevAutomation.Infrastructure.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DevAutomation.Tests;

public sealed class HostingCompositionTests
{
    [Fact]
    public void ApiComposition_DoesNotRegisterKafkaAgentWorker()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddDevAutomationCore(configuration);
        services.AddDevAutomationInfrastructure(configuration);

        Assert.DoesNotContain(services, IsKafkaAgentWorkerRegistration);
    }

    [Fact]
    public void WorkerComposition_RegistersKafkaAgentWorkerAndSharedInfrastructure()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddDevAutomationCore(configuration);
        services.AddDevAutomationInfrastructure(configuration);
        services.AddDevAutomationAgentWorker();

        Assert.Contains(services, IsKafkaAgentWorkerRegistration);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AgentJob));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITicketQueue));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<QueueOptions>));
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation",
                ["Queue:KafkaBootstrapServers"] = "localhost:9092"
            })
            .Build();
    }

    private static bool IsKafkaAgentWorkerRegistration(ServiceDescriptor descriptor)
    {
        return descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(KafkaAgentWorker);
    }
}
