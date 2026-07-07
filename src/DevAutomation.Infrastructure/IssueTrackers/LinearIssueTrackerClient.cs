using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.IssueTrackers;

public sealed class LinearIssueTrackerClient : IIssueTrackerClient
{
    private const string IssueCreateMutation = """
mutation IssueCreate($input: IssueCreateInput!) {
  issueCreate(input: $input) {
    success
    issue { id identifier url }
  }
}
""";

    private const string CommentCreateMutation = """
mutation CommentCreate($input: CommentCreateInput!) {
  commentCreate(input: $input) {
    success
    comment { id }
  }
}
""";

    private readonly HttpClient _httpClient;
    private readonly LinearOptions _options;

    public LinearIssueTrackerClient(HttpClient httpClient, IOptions<LinearOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public IssueTrackerProvider Provider => IssueTrackerProvider.Linear;

    public async Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        EnsureConfigured(requireTeam: true);
        using var document = await SendGraphQlAsync(IssueCreateMutation, new
        {
            input = new
            {
                teamId = _options.TeamId,
                title = ticket.Title,
                description = ticket.Description
            }
        }, cancellationToken);

        var issue = document.RootElement.GetProperty("data").GetProperty("issueCreate").GetProperty("issue");
        return new ExternalIssueReference(
            issue.GetProperty("id").GetString(),
            issue.GetProperty("identifier").GetString(),
            issue.GetProperty("url").GetString());
    }

    public async Task AddCommentAsync(Ticket ticket, string message, CancellationToken cancellationToken)
    {
        EnsureConfigured(requireTeam: false);
        if (string.IsNullOrWhiteSpace(ticket.ExternalIssueId))
        {
            return;
        }

        using var document = await SendGraphQlAsync(CommentCreateMutation, new
        {
            input = new
            {
                issueId = ticket.ExternalIssueId,
                body = message
            }
        }, cancellationToken);
    }

    private async Task<JsonDocument> SendGraphQlAsync(string query, object variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl);
        request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);
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

    private void EnsureConfigured(bool requireTeam)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiBaseUrl))
        {
            throw new InvalidOperationException("Linear integration requires Linear:ApiKey and Linear:ApiBaseUrl.");
        }

        if (requireTeam && string.IsNullOrWhiteSpace(_options.TeamId))
        {
            throw new InvalidOperationException("Linear issue creation requires Linear:TeamId.");
        }
    }
}
