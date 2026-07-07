namespace DevAutomation.Core.Options;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxConcurrentAgents { get; set; } = 2;
    public TimeSpan AgentTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public string ClaudeImage { get; set; } = "devautomation-claude:latest";
    public string DockerNetwork { get; set; } = "bridge";
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string? GitHubToken { get; set; }
    public string GitAuthorName { get; set; } = "DevAutomation Bot";
    public string GitAuthorEmail { get; set; } = "devautomation@example.local";
    public string ApprovalMcpCommand { get; set; } = "dotnet /app/DevAutomation.ApprovalMcp.dll";
}
