using System.Text.Json;
using DevAutomation.Core.Abstractions;

namespace DevAutomation.Infrastructure.Agents;

public sealed class ClaudeStreamParser
{
    public AgentLogEvent Parse(DateTimeOffset timestamp, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new AgentLogEvent(timestamp, "stdout", string.Empty);
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "stream-json" : "stream-json";
            return new AgentLogEvent(timestamp, type, line);
        }
        catch (JsonException)
        {
            return new AgentLogEvent(timestamp, "stdout", line);
        }
    }
}
