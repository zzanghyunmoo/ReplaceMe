using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.IssueTrackers;

public sealed class JiraIssueTrackerClient : IIssueTrackerClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;

    public JiraIssueTrackerClient(HttpClient httpClient, IOptions<JiraOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public IssueTrackerProvider Provider => IssueTrackerProvider.Jira;

    public async Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        EnsureConfigured(requireProject: true);
        using var request = CreateRequest(HttpMethod.Post, "/rest/api/3/issue");
        request.Content = JsonContent.Create(new
        {
            fields = new
            {
                project = new { key = _options.ProjectKey },
                issuetype = new { name = _options.IssueType },
                summary = ticket.Title,
                description = CreateAdfDocument(ticket.Description)
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var key = root.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : null;
        var url = string.IsNullOrWhiteSpace(key) ? null : $"{_options.BaseUrl.TrimEnd('/')}/browse/{Uri.EscapeDataString(key)}";
        return new ExternalIssueReference(id, key, url);
    }

    public async Task AddCommentAsync(Ticket ticket, string message, CancellationToken cancellationToken)
    {
        EnsureConfigured(requireProject: false);
        var issueKey = ticket.ExternalIssueKey ?? ticket.ExternalIssueId;
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Post, $"/rest/api/3/issue/{Uri.EscapeDataString(issueKey)}/comment");
        request.Content = JsonContent.Create(new { body = CreateAdfDocument(message) });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private void EnsureConfigured(bool requireProject)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new InvalidOperationException("Jira integration requires Jira:BaseUrl, Jira:Email, and Jira:ApiToken.");
        }

        if (requireProject && string.IsNullOrWhiteSpace(_options.ProjectKey))
        {
            throw new InvalidOperationException("Jira issue creation requires Jira:ProjectKey.");
        }
    }

    private static object CreateAdfDocument(string text) => new
    {
        type = "doc",
        version = 1,
        content = new object[]
        {
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new { type = "text", text = string.IsNullOrWhiteSpace(text) ? "DevAutomation update" : text }
                }
            }
        }
    };

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Jira API failed with {(int)response.StatusCode}: {body}");
    }
}
