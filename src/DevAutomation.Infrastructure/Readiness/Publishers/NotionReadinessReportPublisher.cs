using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Publishers;

public sealed class NotionReadinessReportPublisher : IReadinessReportPublisher
{
    private const string Marker = "replaceme-readiness:personal-github-linear-notion";
    private readonly HttpClient _httpClient;
    private readonly NotionOptions _notionOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public NotionReadinessReportPublisher(HttpClient httpClient, IOptions<NotionOptions> notionOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _httpClient = httpClient;
        _notionOptions = notionOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Surface => "Notion";

    public async Task<ProfileReadinessReportSurfaceResult> PublishAsync(ProfileReadinessReport report, CancellationToken cancellationToken)
    {
        var setupPageId = FirstNonEmpty(_profileOptions.Notion.SetupPageId, _notionOptions.ParentPageId);
        if (string.IsNullOrWhiteSpace(_notionOptions.ApiToken) || string.IsNullOrWhiteSpace(setupPageId))
        {
            return ProfileReadinessReportSurfaceResult.Failed(Surface, _profileOptions.Publishers.NotionSeverity, "Notion publisher is missing Notion:ApiToken or ProfileReadiness:Notion:SetupPageId.", "Configure Notion credentials and setup page ID.");
        }

        try
        {
            var existingBlockId = await FindExistingReadinessBlockAsync(setupPageId, cancellationToken);
            if (string.IsNullOrWhiteSpace(existingBlockId))
            {
                await AppendReadinessBlockAsync(setupPageId, report, cancellationToken);
                return ProfileReadinessReportSurfaceResult.Passed(Surface, _profileOptions.Publishers.NotionSeverity, "Notion readiness section was appended.");
            }

            await UpdateReadinessBlockAsync(existingBlockId, report, cancellationToken);
            return ProfileReadinessReportSurfaceResult.Passed(Surface, _profileOptions.Publishers.NotionSeverity, "Notion readiness section was updated.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessReportSurfaceResult.Failed(Surface, _profileOptions.Publishers.NotionSeverity, $"Notion readiness report publishing failed: {ex.Message}", "Verify Notion write permission and setup page ID.");
        }
    }

    private async Task<string?> FindExistingReadinessBlockAsync(string pageId, CancellationToken cancellationToken)
    {
        string? cursor = null;
        do
        {
            var path = string.IsNullOrWhiteSpace(cursor)
                ? $"blocks/{pageId}/children?page_size=100"
                : $"blocks/{pageId}/children?page_size=100&start_cursor={Uri.EscapeDataString(cursor)}";
            using var request = CreateRequest(HttpMethod.Get, new Uri(new Uri(_notionOptions.ApiBaseUrl.TrimEnd('/') + "/"), path));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Notion block children read failed with {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var block in results.EnumerateArray())
            {
                var text = ExtractPlainText(block);
                if (text.Contains(Marker, StringComparison.Ordinal))
                {
                    return block.GetProperty("id").GetString();
                }
            }

            var hasMore = document.RootElement.TryGetProperty("has_more", out var hasMoreElement) && hasMoreElement.GetBoolean();
            cursor = hasMore && document.RootElement.TryGetProperty("next_cursor", out var cursorElement)
                ? cursorElement.GetString()
                : null;
        } while (!string.IsNullOrWhiteSpace(cursor));

        return null;
    }

    private async Task AppendReadinessBlockAsync(string pageId, ProfileReadinessReport report, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Patch, new Uri(new Uri(_notionOptions.ApiBaseUrl.TrimEnd('/') + "/"), $"blocks/{pageId}/children"));
        request.Content = JsonContent.Create(new
        {
            children = new object[]
            {
                Paragraph(RenderReport(report))
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Notion readiness append failed with {(int)response.StatusCode}: {body}");
        }
    }

    private async Task UpdateReadinessBlockAsync(string blockId, ProfileReadinessReport report, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Patch, new Uri(new Uri(_notionOptions.ApiBaseUrl.TrimEnd('/') + "/"), $"blocks/{blockId}"));
        request.Content = JsonContent.Create(new
        {
            paragraph = new
            {
                rich_text = RichText(RenderReport(report))
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Notion readiness update failed with {(int)response.StatusCode}: {body}");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _notionOptions.ApiToken);
        request.Headers.Add("Notion-Version", _notionOptions.NotionVersion);
        return request;
    }

    private static string RenderReport(ProfileReadinessReport report)
    {
        var blocking = report.Checks.Where(x => x.BlocksRun).Select(x => $"- {x.Id}: {x.Summary}");
        var warnings = report.Checks.Where(x => x.Status == ProfileReadinessStatus.Warning).Select(x => $"- {x.Id}: {x.Summary}");
        return $"""
{Marker}
ReplaceMe readiness: {report.ProfileName}
Mode: {report.Mode}
Generated: {report.GeneratedAt:O}
Runnable: {report.IsRunnable}
Summary: {report.Summary}

Blocking checks:
{JoinOrNone(blocking)}

Warnings:
{JoinOrNone(warnings)}
""";
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? "- None" : string.Join(Environment.NewLine, materialized);
    }

    private static object Paragraph(string text) => new
    {
        @object = "block",
        type = "paragraph",
        paragraph = new
        {
            rich_text = RichText(text)
        }
    };

    private static object[] RichText(string text) => [new { type = "text", text = new { content = text.Length > 1900 ? text[..1900] : text } }];

    private static string ExtractPlainText(JsonElement block)
    {
        if (!block.TryGetProperty("type", out var typeElement)) return string.Empty;
        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type) || !block.TryGetProperty(type, out var typedBlock)) return string.Empty;
        if (!typedBlock.TryGetProperty("rich_text", out var richText) || richText.ValueKind != JsonValueKind.Array) return string.Empty;
        return string.Concat(richText.EnumerateArray().Select(x => x.TryGetProperty("plain_text", out var plainText) ? plainText.GetString() : string.Empty));
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}
