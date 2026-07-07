using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.DocumentTools;

public sealed class NotionDocumentToolClient : IDocumentToolClient
{
    private readonly HttpClient _httpClient;
    private readonly NotionOptions _options;

    public NotionDocumentToolClient(HttpClient httpClient, IOptions<NotionOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public DocumentToolProvider Provider => DocumentToolProvider.Notion;

    public async Task<DocumentReference> CreateTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/"), "pages"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        request.Headers.Add("Notion-Version", _options.NotionVersion);
        request.Content = JsonContent.Create(new
        {
            parent = new { page_id = _options.ParentPageId },
            properties = new
            {
                title = new
                {
                    title = new object[] { new { text = new { content = $"DevAutomation: {ticket.Title}" } } }
                }
            },
            children = new object[]
            {
                Paragraph($"Status: {ticket.Status}"),
                Paragraph($"Repository: {ticket.RepoUrl}"),
                Paragraph($"Base branch: {ticket.BaseBranch}"),
                Paragraph($"PR/MR: {ticket.PrUrl ?? "-"}"),
                Paragraph(ticket.Description)
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Notion API failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
        return new DocumentReference(DocumentToolProvider.Notion, id, url);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiToken) || string.IsNullOrWhiteSpace(_options.ParentPageId))
        {
            throw new InvalidOperationException("Notion integration requires Notion:ApiToken and Notion:ParentPageId.");
        }
    }

    private static object Paragraph(string text) => new
    {
        @object = "block",
        type = "paragraph",
        paragraph = new
        {
            rich_text = new object[] { new { type = "text", text = new { content = string.IsNullOrWhiteSpace(text) ? "-" : text } } }
        }
    };
}
