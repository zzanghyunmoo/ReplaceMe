namespace DevAutomation.Core.Options;

public enum CodingAgentProvider
{
    ClaudeCode = 0
}

public sealed class CodingAgentOptions
{
    public const string SectionName = "CodingAgent";

    public CodingAgentProvider Provider { get; set; } = CodingAgentProvider.ClaudeCode;
    public string ClaudeCommand { get; set; } = "claude";
}
