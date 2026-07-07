using DevAutomation.Core.Options;

namespace DevAutomation.Infrastructure.RemoteRepositories;

public sealed class GitLabRemoteRepositoryIntegration : IRemoteRepositoryIntegration
{
    public RemoteRepositoryProvider Provider => RemoteRepositoryProvider.GitLab;

    public void AddEnvironment(ICollection<string> environment, AgentOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.GitLabToken)) environment.Add($"GITLAB_TOKEN={options.GitLabToken}");
        environment.Add($"GITLAB_API_BASE_URL={options.GitLabApiBaseUrl.TrimEnd('/')}");
    }

    public string BuildCreateChangeRequestScript() => """
if [ -z "${GITLAB_TOKEN:-}" ]; then
  echo "Skipping GitLab MR creation because GITLAB_TOKEN is not configured."
else
  project_path=$(node <<'NODE'
const remote = process.env.REPO_URL || '';
function trimGit(value) { return value.replace(/^\//, '').replace(/\.git$/, ''); }
if (/^git@/i.test(remote)) {
  const [, path] = remote.split(':');
  process.stdout.write(trimGit(path || ''));
} else {
  const url = new URL(remote);
  process.stdout.write(trimGit(url.pathname));
}
NODE
)
  encoded_project=$(node -e "process.stdout.write(encodeURIComponent(process.argv[1]))" "$project_path")
  body=$(node <<'NODE'
process.stdout.write(JSON.stringify({
  source_branch: `agent/ticket-${process.env.TICKET_ID}`,
  target_branch: process.env.BASE_BRANCH,
  title: process.env.TICKET_TITLE,
  description: `Automated implementation for ticket ${process.env.TICKET_ID}`,
  remove_source_branch: true
}));
NODE
)
  mr_response=$(curl --fail -sS -X POST "${GITLAB_API_BASE_URL%/}/projects/${encoded_project}/merge_requests" \
    -H "PRIVATE-TOKEN: ${GITLAB_TOKEN}" \
    -H "Content-Type: application/json" \
    --data "$body")
  pr_url=$(printf '%s' "$mr_response" | node -e "let s='';process.stdin.on('data',d=>s+=d);process.stdin.on('end',()=>{const r=JSON.parse(s);process.stdout.write(r.web_url || '')})")
  if [ -n "$pr_url" ]; then
    printf '%s\n' "$pr_url" > /tmp/pr-url
    printf 'PR_URL=%s\n' "$pr_url"
  fi
fi
""";
}
