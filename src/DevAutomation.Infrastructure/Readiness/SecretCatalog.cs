using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness;

public sealed class SecretCatalog : ISecretCatalog
{
    private readonly IConfiguration _configuration;
    private readonly AgentOptions _agentOptions;
    private readonly SlackOptions _slackOptions;
    private readonly JiraOptions _jiraOptions;
    private readonly LinearOptions _linearOptions;
    private readonly GmailOptions _gmailOptions;
    private readonly NotionOptions _notionOptions;
    private readonly ConfluenceOptions _confluenceOptions;

    public SecretCatalog(
        IConfiguration configuration,
        IOptions<AgentOptions> agentOptions,
        IOptions<SlackOptions> slackOptions,
        IOptions<JiraOptions> jiraOptions,
        IOptions<LinearOptions> linearOptions,
        IOptions<GmailOptions> gmailOptions,
        IOptions<NotionOptions> notionOptions,
        IOptions<ConfluenceOptions> confluenceOptions)
    {
        _configuration = configuration;
        _agentOptions = agentOptions.Value;
        _slackOptions = slackOptions.Value;
        _jiraOptions = jiraOptions.Value;
        _linearOptions = linearOptions.Value;
        _gmailOptions = gmailOptions.Value;
        _notionOptions = notionOptions.Value;
        _confluenceOptions = confluenceOptions.Value;
    }

    public IReadOnlyList<SecretCatalogEntry> GetSecrets() =>
    [
        new("Agent:AnthropicApiKey", _agentOptions.AnthropicApiKey),
        new("Agent:GitHubToken", _agentOptions.GitHubToken),
        new("Agent:GitLabToken", _agentOptions.GitLabToken),
        new("ConnectionStrings:Postgres", _configuration.GetConnectionString("Postgres")),
        new("Slack:BotToken", _slackOptions.BotToken),
        new("Slack:SigningSecret", _slackOptions.SigningSecret),
        new("Jira:ApiToken", _jiraOptions.ApiToken),
        new("Linear:ApiKey", _linearOptions.ApiKey),
        new("Gmail:AccessToken", _gmailOptions.AccessToken),
        new("Notion:ApiToken", _notionOptions.ApiToken),
        new("Confluence:ApiToken", _confluenceOptions.ApiToken)
    ];
}
