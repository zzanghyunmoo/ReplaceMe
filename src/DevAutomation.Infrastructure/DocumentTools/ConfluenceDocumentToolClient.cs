using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.DocumentTools;

public sealed class ConfluenceDocumentToolClient : IDocumentToolClient
{
    private readonly HttpClient _httpClient;
    private readonly ConfluenceOptions _options;

    public ConfluenceDocumentToolClient(HttpClient httpClient, IOptions<ConfluenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public DocumentToolProvider Provider => DocumentToolProvider.Confluence;

    public async Task<DocumentReference> CreateTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), "wiki/rest/api/content"));
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
        request.Content = JsonContent.Create(new
        {
            type = "page",
            title = $"DevAutomation: {ticket.Title}",
            space = new { key = _options.SpaceKey },
            ancestors = string.IsNullOrWhiteSpace(_options.ParentPageId) ? Array.Empty<object>() : new object[] { new { id = _options.ParentPageId } },
            body = new
            {
                storage = new
                {
                    value = BuildStorageBody(ticket),
                    representation = "storage"
                }
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Confluence API failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var url = _options.BaseUrl.TrimEnd('/');
        if (root.TryGetProperty("_links", out var links) && links.TryGetProperty("webui", out var webui))
        {
            url += webui.GetString();
        }

        return new DocumentReference(DocumentToolProvider.Confluence, id, url);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.Email)
            || string.IsNullOrWhiteSpace(_options.ApiToken)
            || string.IsNullOrWhiteSpace(_options.SpaceKey))
        {
            throw new InvalidOperationException("Confluence integration requires Confluence:BaseUrl, Confluence:Email, Confluence:ApiToken, and Confluence:SpaceKey.");
        }
    }

    private static string BuildStorageBody(Ticket ticket)
    {
        static string Escape(string? value) => System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);

        return $"""
<h1>{Escape(ticket.Title)}</h1>
<p><strong>Status:</strong> {Escape(ticket.Status.ToString())}</p>
<p><strong>Repository:</strong> {Escape(ticket.RepoUrl)}</p>
<p><strong>Base branch:</strong> {Escape(ticket.BaseBranch)}</p>
<p><strong>PR/MR:</strong> {Escape(ticket.PrUrl)}</p>
<h2>Description</h2>
<p>{Escape(ticket.Description)}</p>
""";
    }
}
