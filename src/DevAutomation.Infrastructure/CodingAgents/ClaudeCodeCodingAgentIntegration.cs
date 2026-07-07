using DevAutomation.Core.Options;

namespace DevAutomation.Infrastructure.CodingAgents;

public sealed class ClaudeCodeCodingAgentIntegration : ICodingAgentIntegration
{
    public CodingAgentProvider Provider => CodingAgentProvider.ClaudeCode;

    public void AddEnvironment(ICollection<string> environment, AgentOptions agentOptions, CodingAgentOptions codingAgentOptions)
    {
        if (!string.IsNullOrWhiteSpace(agentOptions.AnthropicApiKey)) environment.Add($"ANTHROPIC_API_KEY={agentOptions.AnthropicApiKey}");
        environment.Add($"CODING_AGENT_COMMAND={codingAgentOptions.ClaudeCommand}");
    }

    public string BuildRunScript() => """
"${CODING_AGENT_COMMAND:-claude}" -p "$TICKET_PROMPT" --output-format stream-json --mcp-config /tmp/claude-mcp.json --strict-mcp-config --permission-prompt-tool mcp__approval__approval_prompt
""";
}
