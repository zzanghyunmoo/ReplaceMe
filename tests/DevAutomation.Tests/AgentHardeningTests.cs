using System.Reflection;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.CodingAgents;
using DevAutomation.Infrastructure.Readiness;
using DevAutomation.Infrastructure.Readiness.Checks;
using DevAutomation.Infrastructure.RemoteRepositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DevAutomation.Tests;

public sealed class AgentHardeningTests
{
    [Fact]
    public void Production_like_local_docker_socket_without_prod_opt_in_blocks_readiness()
    {
        var result = AgentIsolationReadinessCheck.EvaluateDockerSocketPosture(new AgentOptions
        {
            ExecutionIsolationProfile = AgentExecutionIsolationProfile.ProductionLike,
            DockerSocketMode = AgentDockerSocketMode.LocalDockerSocket,
            AllowLocalDockerSocket = true,
            AllowLocalDockerSocketInProductionLike = false
        });

        Assert.True(result.BlocksRun);
        Assert.Equal("agent.docker.socket.posture", result.Id);
        Assert.Equal(ProfileReadinessStatus.Failed, result.Status);
        Assert.Contains("Production-like", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_environment_name_can_make_socket_posture_production_like()
    {
        var result = AgentIsolationReadinessCheck.EvaluateDockerSocketPosture(
            new AgentOptions
            {
                ExecutionIsolationProfile = AgentExecutionIsolationProfile.LocalDevelopment,
                DockerSocketMode = AgentDockerSocketMode.LocalDockerSocket,
                AllowLocalDockerSocket = true,
                AllowLocalDockerSocketInProductionLike = false
            },
            aspNetCoreEnvironment: "Staging");

        Assert.True(result.BlocksRun);
        Assert.Contains("Production-like", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Local_docker_socket_requires_explicit_local_opt_in()
    {
        var result = AgentIsolationReadinessCheck.EvaluateDockerSocketPosture(new AgentOptions
        {
            ExecutionIsolationProfile = AgentExecutionIsolationProfile.LocalDevelopment,
            DockerSocketMode = AgentDockerSocketMode.LocalDockerSocket,
            AllowLocalDockerSocket = false
        });

        Assert.True(result.BlocksRun);
        Assert.Contains("explicitly opted in", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Local_docker_socket_opt_in_returns_local_only_warning()
    {
        var result = AgentIsolationReadinessCheck.EvaluateDockerSocketPosture(new AgentOptions
        {
            ExecutionIsolationProfile = AgentExecutionIsolationProfile.LocalDevelopment,
            DockerSocketMode = AgentDockerSocketMode.LocalDockerSocket,
            AllowLocalDockerSocket = true
        });

        Assert.False(result.BlocksRun);
        Assert.Equal(ProfileReadinessStatus.Warning, result.Status);
        Assert.Contains("trusted local development", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runner_enforces_production_like_socket_opt_in_even_without_readiness_gate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DockerAgentRunner.EnsureLocalDockerSocketAllowed(
                new AgentOptions
                {
                    ExecutionIsolationProfile = AgentExecutionIsolationProfile.LocalDevelopment,
                    DockerSocketMode = AgentDockerSocketMode.LocalDockerSocket,
                    AllowLocalDockerSocket = true,
                    AllowLocalDockerSocketInProductionLike = false
                },
                aspNetCoreEnvironment: "Staging"));

        Assert.Contains("Production-like", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Secret_catalog_and_redactor_cover_ai_observability_and_gateway_secrets()
    {
        var catalog = new SecretCatalog(
            new ConfigurationBuilder().Build(),
            Options.Create(new AgentOptions()),
            Options.Create(new SlackOptions()),
            Options.Create(new JiraOptions()),
            Options.Create(new LinearOptions()),
            Options.Create(new GmailOptions()),
            Options.Create(new NotionOptions()),
            Options.Create(new ConfluenceOptions()),
            Options.Create(new LangfuseOptions { PublicKey = "pk-lf-test", SecretKey = "sk-lf-test" }),
            Options.Create(new LiteLlmOptions { ApiKey = "litellm-admin-test", VirtualKey = "litellm-virtual-test" }));

        var labels = catalog.GetSecrets().Select(x => x.Label).ToArray();
        Assert.Contains("Langfuse:PublicKey", labels);
        Assert.Contains("Langfuse:SecretKey", labels);
        Assert.Contains("LiteLLM:ApiKey", labels);
        Assert.Contains("LiteLLM:VirtualKey", labels);

        var redactor = new SecretRedactor(catalog.GetSecrets().Select(x => x.Value));
        var redacted = redactor.Redact("pk-lf-test sk-lf-test litellm-admin-test litellm-virtual-test");

        Assert.DoesNotContain("pk-lf-test", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-lf-test", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("litellm-admin-test", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("litellm-virtual-test", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_repository_environment_only_includes_selected_provider_secret()
    {
        var options = new AgentOptions
        {
            GitHubToken = "github-token",
            GitLabToken = "gitlab-token"
        };

        var githubEnvironment = new List<string>();
        new GitHubRemoteRepositoryIntegration().AddEnvironment(githubEnvironment, options);

        Assert.Contains("GITHUB_TOKEN=github-token", githubEnvironment);
        Assert.DoesNotContain(githubEnvironment, value => value.StartsWith("GITLAB_TOKEN=", StringComparison.Ordinal));

        var gitlabEnvironment = new List<string>();
        new GitLabRemoteRepositoryIntegration().AddEnvironment(gitlabEnvironment, options);

        Assert.Contains("GITLAB_TOKEN=gitlab-token", gitlabEnvironment);
        Assert.DoesNotContain(gitlabEnvironment, value => value.StartsWith("GITHUB_TOKEN=", StringComparison.Ordinal));
    }

    [Fact]
    public void Final_agent_container_environment_uses_explicit_allowlist_for_runtime_secrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=postgres;Password=db-secret",
                ["Approval:ApprovalTimeout"] = "00:10:00",
                ["Approval:PollInterval"] = "00:00:02",
                ["Slack:BotToken"] = "xoxb-approval-secret",
                ["Slack:ChannelId"] = "C123",
                ["Slack:ApiBaseUrl"] = "https://slack.example/api/",
                ["Langfuse:SecretKey"] = "langfuse-secret",
                ["LiteLLM:VirtualKey"] = "litellm-secret"
            })
            .Build();
        using var runner = new DockerAgentRunner(
            Options.Create(new AgentOptions
            {
                AnthropicApiKey = "anthropic-token",
                GitHubToken = "github-token",
                GitLabToken = "gitlab-token"
            }),
            Options.Create(new CodingAgentOptions { ClaudeCommand = "claude" }),
            configuration,
            new SecretRedactor([]),
            new GitHubRemoteRepositoryIntegration(),
            new ClaudeCodeCodingAgentIntegration(),
            NullLogger<DockerAgentRunner>.Instance);
        var ticket = Ticket.Create(
            "Test ticket",
            "Do the thing",
            "https://github.com/example/repo.git",
            "main",
            DateTimeOffset.UtcNow);

        var method = typeof(DockerAgentRunner).GetMethod("BuildEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildEnvironment method not found.");
        var environment = Assert.IsAssignableFrom<IList<string>>(method.Invoke(runner, [ticket]));

        Assert.Contains("ANTHROPIC_API_KEY=anthropic-token", environment);
        Assert.Contains("GITHUB_TOKEN=github-token", environment);
        Assert.Contains("DEVAUTOMATION_ConnectionStrings__Postgres=Host=postgres;Password=db-secret", environment);
        Assert.Contains("DEVAUTOMATION_Slack__BotToken=xoxb-approval-secret", environment);
        Assert.Contains("DEVAUTOMATION_Slack__ChannelId=C123", environment);
        Assert.DoesNotContain(environment, value => value.StartsWith("GITLAB_TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain(environment, value => value.Contains("langfuse-secret", StringComparison.Ordinal));
        Assert.DoesNotContain(environment, value => value.Contains("litellm-secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Claude_code_environment_only_includes_direct_provider_secret()
    {
        var environment = new List<string>();
        new ClaudeCodeCodingAgentIntegration().AddEnvironment(
            environment,
            new AgentOptions { AnthropicApiKey = "anthropic-token" },
            new CodingAgentOptions { ClaudeCommand = "claude" });

        Assert.Contains("ANTHROPIC_API_KEY=anthropic-token", environment);
        Assert.DoesNotContain(environment, value => value.StartsWith("LANGFUSE_", StringComparison.Ordinal));
        Assert.DoesNotContain(environment, value => value.StartsWith("LITELLM_", StringComparison.Ordinal));
    }
}
