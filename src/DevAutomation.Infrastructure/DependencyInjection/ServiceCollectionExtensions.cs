using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Options;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.Persistence;
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
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

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
        services.AddHttpClient<IApprovalNotifier, SlackApprovalNotifier>();
        services.AddHttpClient<ITicketNotifier, SlackApprovalNotifier>();
        return services;
    }
}
