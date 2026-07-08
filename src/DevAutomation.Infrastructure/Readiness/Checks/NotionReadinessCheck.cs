using System.Net.Http.Headers;
using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class NotionReadinessCheck : IProfileReadinessCheck
{
    private readonly HttpClient _httpClient;
    private readonly NotionOptions _notionOptions;
    private readonly ProfileReadinessOptions _profileOptions;

    public NotionReadinessCheck(HttpClient httpClient, IOptions<NotionOptions> notionOptions, IOptions<ProfileReadinessOptions> profileOptions)
    {
        _httpClient = httpClient;
        _notionOptions = notionOptions.Value;
        _profileOptions = profileOptions.Value;
    }

    public string Id => "notion.read.access";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        var setupPageId = FirstNonEmpty(_profileOptions.Notion.SetupPageId, _notionOptions.ParentPageId);
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_notionOptions.ApiToken)) missing.Add("Notion:ApiToken");
        if (string.IsNullOrWhiteSpace(setupPageId)) missing.Add("ProfileReadiness:Notion:SetupPageId");

        if (missing.Count > 0)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Notion", ProfileReadinessSeverity.Required, $"Notion readiness is missing required configuration: {string.Join(", ", missing)}.", "Configure Notion token and readiness setup page ID.");
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, new Uri(new Uri(_notionOptions.ApiBaseUrl.TrimEnd('/') + "/"), $"pages/{setupPageId}"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? ProfileReadinessCheckResult.Passed(Id, "Notion", ProfileReadinessSeverity.Required, "Notion setup page is readable.")
                : ProfileReadinessCheckResult.Failed(Id, "Notion", ProfileReadinessSeverity.Required, $"Notion setup page read failed with {(int)response.StatusCode}: {body}", "Verify Notion token permissions and setup page ID.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Notion", ProfileReadinessSeverity.Required, $"Notion read probe failed: {ex.Message}", "Verify Notion API configuration and network access.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _notionOptions.ApiToken);
        request.Headers.Add("Notion-Version", _notionOptions.NotionVersion);
        return request;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}
