using DevAutomation.Core.Options;

namespace DevAutomation.Infrastructure.RemoteRepositories;

public sealed class GitHubRemoteRepositoryIntegration : IRemoteRepositoryIntegration
{
    public RemoteRepositoryProvider Provider => RemoteRepositoryProvider.GitHub;

    public void AddEnvironment(ICollection<string> environment, AgentOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.GitHubToken)) environment.Add($"GITHUB_TOKEN={options.GitHubToken}");
    }

    public string BuildCreateChangeRequestScript() => """
if command -v gh >/dev/null 2>&1; then
  pr_url=$(gh pr create --base "$BASE_BRANCH" --head "$branch" --title "$TICKET_TITLE" --body "Automated implementation for ticket ${TICKET_ID}")
  printf '%s\n' "$pr_url" > /tmp/pr-url
  printf 'PR_URL=%s\n' "$pr_url"
fi
""";
}
