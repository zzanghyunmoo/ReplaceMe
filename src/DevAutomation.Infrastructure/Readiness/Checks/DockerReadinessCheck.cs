using DevAutomation.Core.Readiness;
using Docker.DotNet;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class DockerReadinessCheck : IProfileReadinessCheck
{
    public string Id => "local.docker.ping";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var docker = new DockerClientConfiguration().CreateClient();
            await docker.System.PingAsync(cancellationToken);
            return ProfileReadinessCheckResult.Passed(Id, "Local", ProfileReadinessSeverity.Required, "Docker daemon is reachable.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, $"Docker daemon is not reachable: {ex.Message}", "Start Docker Desktop or configure Docker daemon access.");
        }
    }
}
