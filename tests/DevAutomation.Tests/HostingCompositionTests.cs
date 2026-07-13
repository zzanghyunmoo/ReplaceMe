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

    [Fact]
    public void QueueOptions_DefaultConsumerGroup_RemainsLegacyCompatible()
    {
        var options = new QueueOptions();

        Assert.Equal("devautomation-api", options.KafkaConsumerGroupId);
    }

    [Fact]
    public void WorkerDockerStage_UsesAspNetRuntimeForPublishedWorker()
    {
        var dockerfile = ReadRepoFile("Dockerfile");

        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS worker", dockerfile);
    }

    [Fact]
    public void ComposeWorker_WaitsForMigrationsAndKafkaInsteadOfApiHealth()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var workerService = SliceBetween(compose, "  worker:", "\n  postgres:");

        Assert.Contains("DEVAUTOMATION_Database__ApplyMigrations: \"false\"", workerService);
        Assert.Contains("DEVAUTOMATION_Queue__KafkaConsumerGroupId: devautomation-api", workerService);
        Assert.Contains("migrate:", workerService);
        Assert.Contains("kafka:", workerService);
        Assert.DoesNotContain("api:", workerService);
    }

    [Fact]
    public void ComposeUsesSingleMigrationServiceForApiAndWorkerStartup()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var migrateService = SliceBetween(compose, "  migrate:", "\n  worker:");
        var apiService = SliceBetween(compose, "  api:", "\n  migrate:");

        Assert.Contains("DEVAUTOMATION_Database__RunMigrationsOnly: \"true\"", migrateService);
        Assert.Contains("postgres:", migrateService);
        Assert.Contains("DEVAUTOMATION_Database__ApplyMigrations: \"false\"", apiService);
        Assert.Contains("condition: service_completed_successfully", apiService);
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

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DevAutomation.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }

    private static string SliceBetween(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing marker {startMarker}");
        var end = content.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing marker {endMarker}");
        return content[start..end];
    }

    private static bool IsKafkaAgentWorkerRegistration(ServiceDescriptor descriptor)
    {
        return descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(KafkaAgentWorker);
    }
}
