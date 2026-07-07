using System.Text;
using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Docker.DotNet.Models;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Agents;

public sealed class DockerAgentRunner : IAgentRunner, IDisposable
{
    private readonly DockerClient _dockerClient;
    private readonly AgentOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerAgentRunner> _logger;
    private readonly ClaudeStreamParser _parser = new();
    private readonly SecretRedactor _redactor;

    public DockerAgentRunner(IOptions<AgentOptions> options, IConfiguration configuration, ILogger<DockerAgentRunner> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
        _dockerClient = new DockerClientConfiguration().CreateClient();
        _redactor = new SecretRedactor([
            _options.AnthropicApiKey,
            _options.GitHubToken,
            _configuration["Slack:BotToken"],
            _configuration["Slack:SigningSecret"]
        ]);
    }

    public async Task<AgentRunResult> RunAsync(
        Ticket ticket,
        Func<string, CancellationToken, Task> onContainerStarted,
        Func<AgentLogEvent, CancellationToken, Task> onLog,
        CancellationToken cancellationToken)
    {
        var command = BuildAgentScript();
        var env = BuildEnvironment(ticket);
        var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = _options.ClaudeImage,
            Cmd = ["/bin/sh", "-lc", command],
            Env = env,
            Labels = new Dictionary<string, string>
            {
                ["devautomation.ticket-id"] = ticket.Id.ToString(),
                ["devautomation.ticket-title"] = ticket.Title
            },
            HostConfig = new HostConfig
            {
                AutoRemove = false,
                NetworkMode = string.IsNullOrWhiteSpace(_options.DockerNetwork) ? "bridge" : _options.DockerNetwork
            }
        }, cancellationToken);

        var containerId = response.ID;
        await onContainerStarted(containerId, cancellationToken);
        await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);

        var logTask = StreamLogsAsync(containerId, onLog, cancellationToken);
        var waitTask = _dockerClient.Containers.WaitContainerAsync(containerId, cancellationToken);

        try
        {
            ContainerWaitResponse wait;
            try
            {
                wait = await waitTask.WaitAsync(_options.AgentTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                await StopAsync(ticket.Id, containerId, CancellationToken.None);
                return new AgentRunResult(false, null, $"Agent timed out after {_options.AgentTimeout}.");
            }

            var pullRequestUrl = await logTask;
            return wait.StatusCode == 0
                ? new AgentRunResult(true, pullRequestUrl, null)
                : new AgentRunResult(false, null, $"Agent container exited with code {wait.StatusCode}.");
        }
        finally
        {
            await RemoveContainerAsync(containerId);
        }
    }

    public async Task StopAsync(Guid ticketId, string? containerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"devautomation.ticket-id={ticketId}"] = true }
                }
            }, cancellationToken);
            containerId = containers.FirstOrDefault()?.ID;
        }

        if (string.IsNullOrWhiteSpace(containerId))
        {
            return;
        }

        try
        {
            await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already gone.
        }
    }

    private async Task<string?> StreamLogsAsync(string containerId, Func<AgentLogEvent, CancellationToken, Task> onLog, CancellationToken cancellationToken)
    {
        string? pullRequestUrl = null;
        using var stream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, false, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true,
            Timestamps = false
        }, cancellationToken);

        var buffer = new byte[8192];
        var pending = new StringBuilder();
        while (true)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF)
            {
                break;
            }

            var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
            pending.Append(chunk);
            var lines = pending.ToString().Split('\n');
            pending.Clear();
            pending.Append(lines[^1]);

            foreach (var line in lines.Take(lines.Length - 1))
            {
                var redacted = _redactor.Redact(line.TrimEnd('\r'));
                pullRequestUrl ??= TryExtractPullRequestUrl(redacted);
                await onLog(_parser.Parse(DateTimeOffset.UtcNow, redacted), cancellationToken);
            }
        }

        if (pending.Length > 0)
        {
            var redacted = _redactor.Redact(pending.ToString());
            pullRequestUrl ??= TryExtractPullRequestUrl(redacted);
            await onLog(_parser.Parse(DateTimeOffset.UtcNow, redacted), cancellationToken);
        }

        return pullRequestUrl;
    }

    private static string? TryExtractPullRequestUrl(string line)
    {
        const string marker = "PR_URL=";
        var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var candidate = line[(markerIndex + marker.Length)..].Trim();
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.Contains("/pull/", StringComparison.OrdinalIgnoreCase)
                ? uri.ToString()
                : null;
    }

    private async Task RemoveContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true, RemoveVolumes = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove agent container {ContainerId}.", containerId);
        }
    }

    public void Dispose()
    {
        _dockerClient.Dispose();
    }

    private IList<string> BuildEnvironment(Ticket ticket)
    {
        var prompt = $"Ticket: {ticket.Title}\n\n{ticket.Description}";
        var env = new List<string>
        {
            $"TICKET_ID={ticket.Id}",
            $"TICKET_TITLE={ticket.Title}",
            $"TICKET_PROMPT={prompt}",
            $"REPO_URL={ticket.RepoUrl}",
            $"BASE_BRANCH={ticket.BaseBranch}",
            $"GIT_AUTHOR_NAME={_options.GitAuthorName}",
            $"GIT_AUTHOR_EMAIL={_options.GitAuthorEmail}",
            $"GIT_COMMITTER_NAME={_options.GitAuthorName}",
            $"GIT_COMMITTER_EMAIL={_options.GitAuthorEmail}",
            $"APPROVAL_MCP_COMMAND={_options.ApprovalMcpCommand}"
        };

        if (!string.IsNullOrWhiteSpace(_options.AnthropicApiKey)) env.Add($"ANTHROPIC_API_KEY={_options.AnthropicApiKey}");
        if (!string.IsNullOrWhiteSpace(_options.GitHubToken)) env.Add($"GITHUB_TOKEN={_options.GitHubToken}");

        AddEnvironmentValue(env, "DEVAUTOMATION_ConnectionStrings__Postgres", _configuration.GetConnectionString("Postgres"));
        AddEnvironmentValue(env, "DEVAUTOMATION_Approval__ApprovalTimeout", _configuration["Approval:ApprovalTimeout"]);
        AddEnvironmentValue(env, "DEVAUTOMATION_Approval__PollInterval", _configuration["Approval:PollInterval"]);
        AddEnvironmentValue(env, "DEVAUTOMATION_Slack__BotToken", _configuration["Slack:BotToken"]);
        AddEnvironmentValue(env, "DEVAUTOMATION_Slack__ChannelId", _configuration["Slack:ChannelId"]);
        AddEnvironmentValue(env, "DEVAUTOMATION_Slack__ApiBaseUrl", _configuration["Slack:ApiBaseUrl"]);
        return env;
    }

    private static void AddEnvironmentValue(ICollection<string> environment, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment.Add($"{key}={value}");
        }
    }

    private static string BuildAgentScript() => """
set -eu
workdir=/work
mkdir -p "$workdir"
cd "$workdir"
git clone "$REPO_URL" repo
cd repo
git fetch origin "$BASE_BRANCH"
git checkout "$BASE_BRANCH"
branch="agent/ticket-${TICKET_ID}"
git checkout -b "$branch"
node <<'NODE' > /tmp/claude-mcp.json
const command = process.env.APPROVAL_MCP_COMMAND || "dotnet /app/DevAutomation.ApprovalMcp.dll";
process.stdout.write(JSON.stringify({
  mcpServers: {
    approval: {
      command: "/bin/sh",
      args: ["-lc", command]
    }
  }
}));
NODE
claude -p "$TICKET_PROMPT" --output-format stream-json --mcp-config /tmp/claude-mcp.json --strict-mcp-config --permission-prompt-tool mcp__approval__approval_prompt
if [ -n "$(git status --porcelain)" ]; then
  git add -A
  git commit -m "feat: automate ${TICKET_TITLE}"
  git push origin "$branch"
  if command -v gh >/dev/null 2>&1; then
    pr_url=$(gh pr create --base "$BASE_BRANCH" --head "$branch" --title "$TICKET_TITLE" --body "Automated implementation for ticket ${TICKET_ID}")
    printf '%s\n' "$pr_url" > /tmp/pr-url
    printf 'PR_URL=%s\n' "$pr_url"
  fi
fi
""";
}
