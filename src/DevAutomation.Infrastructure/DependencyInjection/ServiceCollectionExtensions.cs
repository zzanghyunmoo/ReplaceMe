using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Options;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.CodingAgents;
using DevAutomation.Infrastructure.Notifications;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Queues;
using DevAutomation.Infrastructure.RemoteRepositories;
using DevAutomation.Infrastructure.Slack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevAutomation.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevAutomationCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<ApprovalOptions>(configuration.GetSection(ApprovalOptions.SectionName));
        services.Configure<SlackOptions>(configuration.GetSection(SlackOptions.SectionName));
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));
        services.Configure<NotifierOptions>(configuration.GetSection(NotifierOptions.SectionName));
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.Configure<CodingAgentOptions>(configuration.GetSection(CodingAgentOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TicketStateMachine>();
        services.AddScoped<ApprovalService>();
        return services;
    }

    public static IServiceCollection AddDevAutomationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation";

        services.AddDbContext<DevAutomationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApprovalRequestRepository, EfApprovalRequestRepository>();
        services.AddScoped<AgentJob>();
        services.AddSingleton<IAgentRunner, DockerAgentRunner>();
        services.AddScoped<SlackInteractivityService>();
        services.AddSingleton<ISlackSignatureVerifier, SlackSignatureVerifier>();
        services.AddScoped<ITicketQueue, KafkaTicketQueue>();

        var notifierOptions = configuration.GetSection(NotifierOptions.SectionName).Get<NotifierOptions>() ?? new NotifierOptions();
        switch (notifierOptions.Provider)
        {
            case NotifierProvider.None:
                services.AddSingleton<NoOpNotifier>();
                services.AddSingleton<ITicketNotifier>(sp => sp.GetRequiredService<NoOpNotifier>());
                services.AddSingleton<IApprovalNotifier>(sp => sp.GetRequiredService<NoOpNotifier>());
                break;
            case NotifierProvider.Gmail:
                services.AddHttpClient<GmailNotifier>();
                services.AddScoped<ITicketNotifier>(sp => sp.GetRequiredService<GmailNotifier>());
                services.AddScoped<IApprovalNotifier>(sp => sp.GetRequiredService<GmailNotifier>());
                break;
            default:
                services.AddHttpClient<SlackApprovalNotifier>();
                services.AddScoped<ITicketNotifier>(sp => sp.GetRequiredService<SlackApprovalNotifier>());
                services.AddScoped<IApprovalNotifier>(sp => sp.GetRequiredService<SlackApprovalNotifier>());
                break;
        }

        var codingAgentOptions = configuration.GetSection(CodingAgentOptions.SectionName).Get<CodingAgentOptions>() ?? new CodingAgentOptions();
        switch (codingAgentOptions.Provider)
        {
            default:
                services.AddSingleton<ICodingAgentIntegration, ClaudeCodeCodingAgentIntegration>();
                break;
        }

        var agentOptions = configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
        switch (agentOptions.RemoteRepositoryProvider)
        {
            case RemoteRepositoryProvider.GitLab:
                services.AddSingleton<IRemoteRepositoryIntegration, GitLabRemoteRepositoryIntegration>();
                break;
            default:
                services.AddSingleton<IRemoteRepositoryIntegration, GitHubRemoteRepositoryIntegration>();
                break;
        }

        return services;
    }
}
