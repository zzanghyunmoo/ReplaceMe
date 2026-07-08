using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class AgentImageReadinessCheck : IProfileReadinessCheck
{
    private readonly AgentOptions _options;

    public AgentImageReadinessCheck(IOptions<AgentOptions> options)
    {
        _options = options.Value;
    }

    public string Id => "agent.image.available";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClaudeImage))
        {
            return ProfileReadinessCheckResult.Failed(Id, "Agent", ProfileReadinessSeverity.Required, "Agent image is not configured.", "Set Agent:ClaudeImage.");
        }

        try
        {
            using var docker = new DockerClientConfiguration().CreateClient();
            var images = await docker.Images.ListImagesAsync(new ImagesListParameters { All = true }, cancellationToken);
            var found = images.Any(x => x.RepoTags?.Any(tag => string.Equals(tag, _options.ClaudeImage, StringComparison.OrdinalIgnoreCase)) == true);
            return found
                ? ProfileReadinessCheckResult.Passed(Id, "Agent", ProfileReadinessSeverity.Required, $"Agent image '{_options.ClaudeImage}' is available.")
                : ProfileReadinessCheckResult.Failed(Id, "Agent", ProfileReadinessSeverity.Required, $"Agent image '{_options.ClaudeImage}' was not found locally.", "Build or pull the configured agent image before starting an agent run.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Agent", ProfileReadinessSeverity.Required, $"Agent image check failed: {ex.Message}", "Verify Docker is running and the configured agent image is available.");
        }
    }
}
