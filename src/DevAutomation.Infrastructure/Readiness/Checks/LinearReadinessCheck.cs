using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class LinearReadinessCheck : IProfileReadinessCheck
{
    private const string ReadQuery = """
query Readiness($teamId: String!, $projectId: String!, $issueId: String!) {
  viewer { id }
  team(id: $teamId) { id name }
  project(id: $projectId) { id name }
  issue(id: $issueId) { id identifier title }
}
""";

    private readonly HttpClient _httpClient;
    private readonly LinearOptions _linearOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public LinearReadinessCheck(HttpClient httpClient, IOptions<LinearOptions> linearOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _httpClient = httpClient;
        _linearOptions = linearOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Id => "linear.read.access";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        var teamId = FirstNonEmpty(_profileOptions.Linear.TeamId, _linearOptions.TeamId);
        var projectId = _profileOptions.Linear.ProjectId;
        var issueId = _profileOptions.Linear.ReadinessIssueId;
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_linearOptions.ApiKey)) missing.Add("Linear:ApiKey");
        if (string.IsNullOrWhiteSpace(teamId)) missing.Add("ProfileReadiness:Linear:TeamId");
        if (string.IsNullOrWhiteSpace(projectId)) missing.Add("ProfileReadiness:Linear:ProjectId");
        if (string.IsNullOrWhiteSpace(issueId)) missing.Add("ProfileReadiness:Linear:ReadinessIssueId");

        if (missing.Count > 0)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Linear", ProfileReadinessSeverity.Required, $"Linear readiness is missing required configuration: {string.Join(", ", missing)}.", "Configure Linear token, team, project, and readiness issue IDs.");
        }

        try
        {
            using var document = await SendGraphQlAsync(ReadQuery, new { teamId, projectId, issueId }, cancellationToken);
            var data = document.RootElement.GetProperty("data");
            if (data.GetProperty("team").ValueKind == JsonValueKind.Null
                || data.GetProperty("project").ValueKind == JsonValueKind.Null
                || data.GetProperty("issue").ValueKind == JsonValueKind.Null)
            {
                return ProfileReadinessCheckResult.Failed(Id, "Linear", ProfileReadinessSeverity.Required, "Linear token could not read the configured team, project, or readiness issue.", "Verify ProfileReadiness:Linear target IDs and Linear API permissions.");
            }

            return ProfileReadinessCheckResult.Passed(Id, "Linear", ProfileReadinessSeverity.Required, "Linear team, project, and readiness issue are readable.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Linear", ProfileReadinessSeverity.Required, $"Linear read probe failed: {ex.Message}", "Verify Linear API key, target IDs, and network access.");
        }
    }

    private async Task<JsonDocument> SendGraphQlAsync(string query, object variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _linearOptions.ApiBaseUrl);
        request.Headers.TryAddWithoutValidation("Authorization", _linearOptions.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new { query, variables });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Linear API failed with {(int)response.StatusCode}: {body}");
        }

        var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            document.Dispose();
            throw new InvalidOperationException($"Linear API returned GraphQL errors: {errors}");
        }

        return document;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}
