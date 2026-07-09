using System.Net.Http.Headers;
using System.Text.Json;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class GitHubRepositoryReadinessCheck : IProfileReadinessCheck
{
    private readonly HttpClient _httpClient;
    private readonly AgentOptions _agentOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public GitHubRepositoryReadinessCheck(HttpClient httpClient, IOptions<AgentOptions> agentOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _httpClient = httpClient;
        _agentOptions = agentOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Id => "github.repo.access";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_agentOptions.GitHubToken) || string.IsNullOrWhiteSpace(_profileOptions.GitHub.RepositoryUrl))
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, "GitHub repository readiness is missing Agent:GitHubToken or ProfileReadiness:GitHub:RepositoryUrl.", "Configure a GitHub token and repository URL for this profile.");
        }

        if (!TryGetOwnerAndRepo(_profileOptions.GitHub.RepositoryUrl, out var owner, out var repo))
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, "GitHub repository URL could not be parsed.", "Use an HTTPS or SSH GitHub repository URL such as https://github.com/owner/repo.git.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _agentOptions.GitHubToken);
            request.Headers.UserAgent.ParseAdd("ReplaceMe-DevAutomation/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub repository read failed with {(int)response.StatusCode}: {body}", "Verify repository URL, token permissions, and network access.");
            }

            using var document = JsonDocument.Parse(body);
            if (!HasPushPermission(document.RootElement))
            {
                return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub token can read {owner}/{repo}, but push permission could not be confirmed.", "Use a token with repository contents write access so the agent can push branches and open PRs.");
            }

            return ProfileReadinessCheckResult.Passed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub repository {owner}/{repo} is readable and push permission is confirmed.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "GitHub", ProfileReadinessSeverity.Required, $"GitHub repository read probe failed: {ex.Message}", "Verify repository URL, token permissions, and network access.");
        }
    }

    private static bool HasPushPermission(JsonElement root)
    {
        return root.TryGetProperty("permissions", out var permissions)
            && permissions.TryGetProperty("push", out var push)
            && push.ValueKind == JsonValueKind.True;
    }

    private static bool TryGetOwnerAndRepo(string repositoryUrl, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;
        var value = repositoryUrl.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                owner = parts[0];
                repo = TrimGitSuffix(parts[1]);
                return true;
            }
        }

        const string sshPrefix = "git@github.com:";
        if (value.StartsWith(sshPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = value[sshPrefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                owner = parts[0];
                repo = TrimGitSuffix(parts[1]);
                return true;
            }
        }

        return false;
    }

    private static string TrimGitSuffix(string value) => value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}
