using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class AgentIsolationReadinessCheck : IProfileReadinessCheck
{
    private readonly AgentOptions _agentOptions;
    private readonly IConfiguration _configuration;

    public AgentIsolationReadinessCheck(IOptions<AgentOptions> agentOptions, IConfiguration configuration)
    {
        _agentOptions = agentOptions.Value;
        _configuration = configuration;
    }

    public string Id => "agent.docker.socket.posture";

    public Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(EvaluateDockerSocketPosture(
            _agentOptions,
            _configuration["ASPNETCORE_ENVIRONMENT"],
            _configuration["DOTNET_ENVIRONMENT"]));
    }

    public static ProfileReadinessCheckResult EvaluateDockerSocketPosture(
        AgentOptions options,
        string? aspNetCoreEnvironment = null,
        string? dotNetEnvironment = null)
    {
        if (options.DockerSocketMode == AgentDockerSocketMode.Disabled)
        {
            return ProfileReadinessCheckResult.Failed(
                "agent.docker.socket.posture",
                "Agent isolation",
                ProfileReadinessSeverity.Required,
                "Agent Docker socket execution is disabled, but no alternate runner is configured.",
                "Use local Docker socket mode for local development or configure an isolated runner before enabling production-like execution.");
        }

        var productionLike = IsProductionLike(options, aspNetCoreEnvironment, dotNetEnvironment);
        if (!options.AllowLocalDockerSocket)
        {
            return ProfileReadinessCheckResult.Failed(
                "agent.docker.socket.posture",
                "Agent isolation",
                ProfileReadinessSeverity.Required,
                productionLike
                    ? "Production-like agent execution cannot use the host Docker socket without explicit opt-in."
                    : "Local Docker socket execution is not explicitly opted in.",
                "Set Agent:DockerSocketMode=LocalDockerSocket and Agent:AllowLocalDockerSocket=true only for trusted local development; production-like use also requires Agent:AllowLocalDockerSocketInProductionLike=true.");
        }

        if (productionLike && !options.AllowLocalDockerSocketInProductionLike)
        {
            return ProfileReadinessCheckResult.Failed(
                "agent.docker.socket.posture",
                "Agent isolation",
                ProfileReadinessSeverity.Required,
                "Production-like agent execution is configured with the local host Docker socket, but production-like opt-in is false.",
                "Prefer an isolated runner. If this is a controlled break-glass environment, set Agent:AllowLocalDockerSocketInProductionLike=true and document the exception.");
        }

        return ProfileReadinessCheckResult.Warning(
            "agent.docker.socket.posture",
            "Agent isolation",
            productionLike
                ? "Production-like agent execution is explicitly opted into the local host Docker socket. Treat this as a temporary exception."
                : "Local Docker socket agent execution is enabled. This mode is for trusted local development only.",
            productionLike
                ? "Move shared/production-like runs to an isolated runner without host Docker socket access."
                : "Do not reuse this local-only Docker socket posture for shared or production-like environments.");
    }

    private static bool IsProductionLike(AgentOptions options, string? aspNetCoreEnvironment, string? dotNetEnvironment)
    {
        if (options.ExecutionIsolationProfile == AgentExecutionIsolationProfile.ProductionLike)
        {
            return true;
        }

        return IsProductionEnvironmentName(aspNetCoreEnvironment) || IsProductionEnvironmentName(dotNetEnvironment);
    }

    private static bool IsProductionEnvironmentName(string? environment)
    {
        return !string.IsNullOrWhiteSpace(environment)
            && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(environment, "LocalDevelopment", StringComparison.OrdinalIgnoreCase);
    }
}
