namespace DevAutomation.Core.Options;

public enum AgentExecutionIsolationProfile
{
    LocalDevelopment = 0,
    ProductionLike = 1
}

public enum AgentDockerSocketMode
{
    LocalDockerSocket = 0,
    Disabled = 1
}

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxConcurrentAgents { get; set; } = 2;
    public TimeSpan AgentTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public string ClaudeImage { get; set; } = "devautomation-claude:latest";
    public string DockerNetwork { get; set; } = "bridge";
    public AgentExecutionIsolationProfile ExecutionIsolationProfile { get; set; } = AgentExecutionIsolationProfile.LocalDevelopment;
    public AgentDockerSocketMode DockerSocketMode { get; set; } = AgentDockerSocketMode.LocalDockerSocket;
    public bool AllowLocalDockerSocket { get; set; }
    public bool AllowLocalDockerSocketInProductionLike { get; set; }
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string? GitHubToken { get; set; }
    public string? GitLabToken { get; set; }
    public string GitLabApiBaseUrl { get; set; } = "https://gitlab.com/api/v4";
    public RemoteRepositoryProvider RemoteRepositoryProvider { get; set; } = RemoteRepositoryProvider.GitHub;
    public string GitAuthorName { get; set; } = "DevAutomation Bot";
    public string GitAuthorEmail { get; set; } = "devautomation@example.local";
    public string ApprovalMcpCommand { get; set; } = "dotnet /app/DevAutomation.ApprovalMcp.dll";
}
