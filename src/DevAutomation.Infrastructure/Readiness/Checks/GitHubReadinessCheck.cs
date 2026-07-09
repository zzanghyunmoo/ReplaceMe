using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class GitHubReadinessCheck : IProfileReadinessCheck
{
    private readonly AgentOptions _agentOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public GitHubReadinessCheck(IOptions<AgentOptions> agentOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _agentOptions = agentOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Id => "github.agent.gh.capability";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        if (_agentOptions.RemoteRepositoryProvider != RemoteRepositoryProvider.GitHub) missing.Add("Agent:RemoteRepositoryProvider=GitHub");
        if (string.IsNullOrWhiteSpace(_agentOptions.ClaudeImage)) missing.Add("Agent:ClaudeImage");

        if (missing.Count > 0)
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub readiness is missing required configuration: {string.Join(", ", missing)}.", "Configure GitHub repository, token, remote provider, and agent image.");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var docker = new DockerClientConfiguration().CreateClient();
            var container = await docker.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = _agentOptions.ClaudeImage,
                Entrypoint = ["/bin/sh", "-lc"],
                Cmd = ["command -v git >/dev/null 2>&1 && command -v gh >/dev/null 2>&1"],
                HostConfig = new HostConfig { AutoRemove = false, NetworkMode = "none" }
            }, timeout.Token);

            try
            {
                await docker.Containers.StartContainerAsync(container.ID, new ContainerStartParameters(), timeout.Token);
                var wait = await docker.Containers.WaitContainerAsync(container.ID, timeout.Token);
                return wait.StatusCode == 0
                    ? ProfileReadinessCheckResult.Passed(Id, "GitHub", ProfileReadinessSeverity.Required, "Agent image contains git and gh commands.")
                    : ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, "Agent image does not provide both git and gh commands.", "Rebuild the agent image with git and GitHub CLI installed.");
            }
            finally
            {
                await docker.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters { Force = true, RemoveVolumes = true }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, "GitHub agent capability check timed out after 15 seconds.", "Verify the agent image entrypoint and Docker runtime health.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub agent capability check failed: {ex.Message}", "Verify Docker, the agent image, and GitHub configuration.");
        }
    }
}
