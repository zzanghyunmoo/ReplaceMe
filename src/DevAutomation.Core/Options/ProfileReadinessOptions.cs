using DevAutomation.Core.Readiness;

namespace DevAutomation.Core.Options;

public sealed class ProfileReadinessOptions
{
    public const string SectionName = "ProfileReadiness";
    public const string PersonalGitHubLinearNotionProfile = "personal-github-linear-notion";

    public string SelectedProfile { get; set; } = string.Empty;
    public GitHubReadinessOptions GitHub { get; set; } = new();
    public LinearReadinessOptions Linear { get; set; } = new();
    public NotionReadinessOptions Notion { get; set; } = new();
    public ReadinessPublisherOptions Publishers { get; set; } = new();
    public ReadinessCheckOptions Checks { get; set; } = new();

    public bool IsPreRunGateEnabled => string.Equals(SelectedProfile, PersonalGitHubLinearNotionProfile, StringComparison.OrdinalIgnoreCase);
}

public sealed class GitHubReadinessOptions
{
    public string RepositoryUrl { get; set; } = string.Empty;
}

public sealed class LinearReadinessOptions
{
    public string TeamId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ReadinessIssueId { get; set; } = string.Empty;
}

public sealed class NotionReadinessOptions
{
    public string SetupPageId { get; set; } = string.Empty;
}

public sealed class ReadinessPublisherOptions
{
    public ProfileReadinessSeverity LinearSeverity { get; set; } = ProfileReadinessSeverity.Required;
    public ProfileReadinessSeverity NotionSeverity { get; set; } = ProfileReadinessSeverity.Required;
}

public sealed class ReadinessCheckOptions
{
    public ProfileReadinessSeverity SecretsRedactionSeverity { get; set; } = ProfileReadinessSeverity.Warning;
}
