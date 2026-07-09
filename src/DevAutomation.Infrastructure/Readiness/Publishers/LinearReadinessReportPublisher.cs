using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Publishers;

public sealed class LinearReadinessReportPublisher : IReadinessReportPublisher
{
    private const string Marker = "<!-- replaceme-readiness:personal-github-linear-notion -->";

    private const string CommentsQuery = """
query ReadinessComments($issueId: String!) {
  issue(id: $issueId) {
    comments { nodes { id body createdAt } }
  }
}
""";

    private const string CommentCreateMutation = """
mutation CommentCreate($input: CommentCreateInput!) {
  commentCreate(input: $input) { success comment { id } }
}
""";

    private const string CommentUpdateMutation = """
mutation CommentUpdate($id: String!, $input: CommentUpdateInput!) {
  commentUpdate(id: $id, input: $input) { success comment { id } }
}
""";

    private readonly HttpClient _httpClient;
    private readonly LinearOptions _linearOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public LinearReadinessReportPublisher(HttpClient httpClient, IOptions<LinearOptions> linearOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _httpClient = httpClient;
        _linearOptions = linearOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Surface => "Linear";

    public async Task<ProfileReadinessReportSurfaceResult> PublishAsync(ProfileReadinessReport report, CancellationToken cancellationToken)
    {
        var issueId = _profileOptions.Linear.ReadinessIssueId;
        if (string.IsNullOrWhiteSpace(_linearOptions.ApiKey) || string.IsNullOrWhiteSpace(issueId))
        {
            return ProfileReadinessReportSurfaceResult.Failed(Surface, _profileOptions.Publishers.LinearSeverity, "Linear publisher is missing Linear:ApiKey or ProfileReadiness:Linear:ReadinessIssueId.", "Configure Linear credentials and readiness issue ID.");
        }

        var body = RenderReport(report);
        try
        {
            var existingCommentId = await FindExistingReadinessCommentAsync(issueId, cancellationToken);
            if (string.IsNullOrWhiteSpace(existingCommentId))
            {
                await SendGraphQlAsync(CommentCreateMutation, new { input = new { issueId, body } }, cancellationToken);
                return ProfileReadinessReportSurfaceResult.Passed(Surface, _profileOptions.Publishers.LinearSeverity, "Linear readiness comment was created.");
            }

            await SendGraphQlAsync(CommentUpdateMutation, new { id = existingCommentId, input = new { body } }, cancellationToken);
            return ProfileReadinessReportSurfaceResult.Passed(Surface, _profileOptions.Publishers.LinearSeverity, "Linear readiness comment was updated.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessReportSurfaceResult.Failed(Surface, _profileOptions.Publishers.LinearSeverity, $"Linear readiness report publishing failed: {ex.Message}", "Verify Linear write permission and readiness issue ID.");
        }
    }

    private async Task<string?> FindExistingReadinessCommentAsync(string issueId, CancellationToken cancellationToken)
    {
        using var document = await SendGraphQlAsync(CommentsQuery, new { issueId }, cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("issue", out var issue)
            || issue.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var nodes = issue.GetProperty("comments").GetProperty("nodes");
        return nodes.EnumerateArray()
            .Where(x => x.TryGetProperty("body", out var body) && (body.GetString() ?? string.Empty).Contains(Marker, StringComparison.Ordinal))
            .OrderByDescending(x => x.TryGetProperty("createdAt", out var createdAt) ? createdAt.GetString() : string.Empty)
            .Select(x => x.GetProperty("id").GetString())
            .FirstOrDefault();
    }

    private async Task<JsonDocument> SendGraphQlAsync(string query, object variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _linearOptions.ApiBaseUrl);
        request.Headers.TryAddWithoutValidation("Authorization", _linearOptions.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new { query, variables });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Linear API failed with {(int)response.StatusCode}: {responseBody}");
        }

        var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            document.Dispose();
            throw new InvalidOperationException($"Linear API returned GraphQL errors: {errors}");
        }

        return document;
    }

    private static string RenderReport(ProfileReadinessReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Marker);
        builder.AppendLine($"## ReplaceMe readiness: `{report.ProfileName}`");
        builder.AppendLine();
        builder.AppendLine($"- Mode: `{report.Mode}`");
        builder.AppendLine($"- Generated: `{report.GeneratedAt:O}`");
        builder.AppendLine($"- Runnable: `{report.IsRunnable}`");
        builder.AppendLine($"- Summary: {report.Summary}");
        builder.AppendLine();
        builder.AppendLine("### Blocking checks");
        var blockers = report.Checks.Where(x => x.BlocksRun).ToArray();
        if (blockers.Length == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var check in blockers)
            {
                builder.AppendLine($"- `{check.Id}` — {check.Summary} Fix: {check.RepairHint ?? "See local report."}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Warnings");
        var warnings = report.Checks.Where(x => x.Status == ProfileReadinessStatus.Warning).ToArray();
        if (warnings.Length == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var check in warnings)
            {
                builder.AppendLine($"- `{check.Id}` — {check.Summary}");
            }
        }

        return builder.ToString();
    }
}
