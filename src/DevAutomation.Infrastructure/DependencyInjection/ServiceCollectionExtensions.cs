using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.CodingAgents;
using DevAutomation.Infrastructure.DocumentTools;
using DevAutomation.Infrastructure.IssueTrackers;
using DevAutomation.Infrastructure.Notifications;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Queues;
using DevAutomation.Infrastructure.Readiness;
using DevAutomation.Infrastructure.Readiness.Checks;
using DevAutomation.Infrastructure.Readiness.Publishers;
using DevAutomation.Infrastructure.RemoteRepositories;
using DevAutomation.Infrastructure.Slack;
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
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.Configure<IssueTrackerOptions>(configuration.GetSection(IssueTrackerOptions.SectionName));
        services.Configure<JiraOptions>(configuration.GetSection(JiraOptions.SectionName));
        services.Configure<LinearOptions>(configuration.GetSection(LinearOptions.SectionName));
        services.Configure<NotifierOptions>(configuration.GetSection(NotifierOptions.SectionName));
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.Configure<DocumentToolOptions>(configuration.GetSection(DocumentToolOptions.SectionName));
        services.Configure<NotionOptions>(configuration.GetSection(NotionOptions.SectionName));
        services.Configure<ConfluenceOptions>(configuration.GetSection(ConfluenceOptions.SectionName));
        services.Configure<CodingAgentOptions>(configuration.GetSection(CodingAgentOptions.SectionName));
        services.Configure<ProfileReadinessOptions>(configuration.GetSection(ProfileReadinessOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TicketStateMachine>();
        services.AddScoped<ApprovalService>();
        return services;
    }

    public static IServiceCollection AddDevAutomationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DevAutomationDbContext>(options => options.UsePostgresDatabase(configuration));
        services.AddScoped<IApprovalRequestRepository, EfApprovalRequestRepository>();
        services.AddScoped<AgentJob>();
        services.AddSingleton<IAgentRunner, DockerAgentRunner>();
        services.AddScoped<SlackInteractivityService>();
        services.AddSingleton<ISlackSignatureVerifier, SlackSignatureVerifier>();
        services.AddScoped<IIssueTrackerService, IssueTrackerService>();
        services.AddScoped<IDocumentToolService, DocumentToolService>();
        services.AddScoped<ITicketQueue, KafkaTicketQueue>();
        services.AddSingleton<ISecretCatalog, SecretCatalog>();
        services.AddSingleton(sp => new SecretRedactor(sp.GetRequiredService<ISecretCatalog>().GetSecrets().Select(x => x.Value)));
        services.AddScoped<IProfileReadinessService, ProfileReadinessService>();
        services.AddScoped<IProfileReadinessCheck, PostgresReadinessCheck>();
        services.AddScoped<IProfileReadinessCheck, KafkaReadinessCheck>();
        services.AddScoped<IProfileReadinessCheck, DockerReadinessCheck>();
        services.AddScoped<IProfileReadinessCheck, AgentImageReadinessCheck>();
        services.AddHttpClient<IProfileReadinessCheck, GitHubRepositoryReadinessCheck>();
        services.AddScoped<IProfileReadinessCheck, GitHubReadinessCheck>();
        services.AddHttpClient<IProfileReadinessCheck, LinearReadinessCheck>();
        services.AddHttpClient<IProfileReadinessCheck, NotionReadinessCheck>();
        services.AddScoped<IProfileReadinessCheck, SecretRedactionReadinessCheck>();
        services.AddHttpClient<IReadinessReportPublisher, LinearReadinessReportPublisher>();
        services.AddHttpClient<IReadinessReportPublisher, NotionReadinessReportPublisher>();

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

        var issueTrackerOptions = configuration.GetSection(IssueTrackerOptions.SectionName).Get<IssueTrackerOptions>() ?? new IssueTrackerOptions();
        switch (issueTrackerOptions.Provider)
        {
            case IssueTrackerProvider.Jira:
                services.AddHttpClient<IIssueTrackerClient, JiraIssueTrackerClient>();
                break;
            case IssueTrackerProvider.Linear:
                services.AddHttpClient<IIssueTrackerClient, LinearIssueTrackerClient>();
                break;
        }

        var documentToolOptions = configuration.GetSection(DocumentToolOptions.SectionName).Get<DocumentToolOptions>() ?? new DocumentToolOptions();
        switch (documentToolOptions.Provider)
        {
            case DocumentToolProvider.Notion:
                services.AddHttpClient<IDocumentToolClient, NotionDocumentToolClient>();
                break;
            case DocumentToolProvider.Confluence:
                services.AddHttpClient<IDocumentToolClient, ConfluenceDocumentToolClient>();
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
