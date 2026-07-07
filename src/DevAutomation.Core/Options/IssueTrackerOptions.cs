using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Options;

public sealed class IssueTrackerOptions
{
    public const string SectionName = "IssueTracker";

    public IssueTrackerProvider Provider { get; set; } = IssueTrackerProvider.None;
}

public sealed class JiraOptions
{
    public const string SectionName = "Jira";

    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
    public string IssueType { get; set; } = "Task";
}

public sealed class LinearOptions
{
    public const string SectionName = "Linear";

    public string ApiKey { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.linear.app/graphql";
}
