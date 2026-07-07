using DevAutomation.Core.Options;

namespace DevAutomation.Infrastructure.CodingAgents;

public interface ICodingAgentIntegration
{
    CodingAgentProvider Provider { get; }

    void AddEnvironment(ICollection<string> environment, AgentOptions agentOptions, CodingAgentOptions codingAgentOptions);

    string BuildRunScript();
}
