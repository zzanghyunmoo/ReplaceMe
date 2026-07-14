using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Contracts;

public sealed class RunPassportProjectionPolicy
{
    private readonly HashSet<string> _jiraHosts;

    public RunPassportProjectionPolicy(IEnumerable<string>? jiraBaseUrls = null)
    {
        _jiraHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in jiraBaseUrls ?? [])
        {
            var host = ExtractHost(value);
            if (host is not null)
            {
                _jiraHosts.Add(host);
            }
        }
    }

    public static RunPassportProjectionPolicy Default { get; } = new();

    internal bool IsIssueHostAllowed(IssueTrackerProvider? provider, string host) => provider switch
    {
        IssueTrackerProvider.Linear => string.Equals(host, "linear.app", StringComparison.OrdinalIgnoreCase),
        IssueTrackerProvider.Jira => _jiraHosts.Contains(host),
        _ => false
    };

    private static string? ExtractHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.IdnHost;
        }

        return candidate.Contains('/') || candidate.Contains(':') ? null : candidate;
    }
}

public sealed record RunPassportSummaryResponse(
    string ContractVersion,
    string RunPassportId,
    string RunPassportUrl,
    Guid TicketId,
    string Title,
    string Status,
    string Summary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset LastLifecycleAt,
    string? IssueTracker,
    string? ExternalIssueKey,
    string? ExternalIssueUrl,
    string? PullRequestUrl,
    string? NotionDocumentId,
    string? NotionDocumentUrl,
    string? TestSummary,
    string? ResidualRiskSummary,
    string? FailureReason)
{
    private static readonly HashSet<string> CredentialQueryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth",
        "key"
    };

    private static readonly string[] CredentialQueryMarkers =
    [
        "accesskey",
        "accesstoken",
        "apikey",
        "authorization",
        "credential",
        "password",
        "secret",
        "signature",
        "token"
    ];

    public const string CurrentContractVersion = "run-passport-summary/v1";
    private const string FailedSummary = "Execution failed.";
    private const string CancelledSummary = "Execution cancelled.";

    public static RunPassportSummaryResponse From(
        Ticket ticket,
        RunPassportProjectionPolicy? projectionPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var policy = projectionPolicy ?? RunPassportProjectionPolicy.Default;
        var externalIssueUrl = NormalizeExternalUrl(
            ticket.ExternalIssueUrl,
            host => policy.IsIssueHostAllowed(ticket.IssueTracker, host));
        string? pullRequestUrl = null;
        if (!string.IsNullOrWhiteSpace(ticket.PrUrl))
        {
            var repositoryHost = ExtractRepositoryHost(ticket.RepoUrl);
            pullRequestUrl = NormalizeExternalUrl(
                ticket.PrUrl,
                host => repositoryHost is not null
                    && string.Equals(host, repositoryHost, StringComparison.OrdinalIgnoreCase));
        }
        var publicFailureReason = GetPublicFailureReason(ticket.Status);

        return new RunPassportSummaryResponse(
            ContractVersion: CurrentContractVersion,
            RunPassportId: $"ticket:{ticket.Id}",
            RunPassportUrl: $"/api/tickets/{ticket.Id}/run-passport",
            TicketId: ticket.Id,
            Title: ticket.Title,
            Status: FormatStatus(ticket.Status),
            Summary: BuildSummary(ticket, pullRequestUrl),
            CreatedAt: ticket.CreatedAt,
            StartedAt: ticket.StartedAt,
            CompletedAt: ticket.CompletedAt,
            LastLifecycleAt: ticket.CompletedAt ?? ticket.StartedAt ?? ticket.CreatedAt,
            IssueTracker: FormatIssueTracker(ticket.IssueTracker),
            ExternalIssueKey: ticket.ExternalIssueKey,
            ExternalIssueUrl: externalIssueUrl,
            PullRequestUrl: pullRequestUrl,
            NotionDocumentId: null,
            NotionDocumentUrl: null,
            TestSummary: null,
            ResidualRiskSummary: null,
            FailureReason: publicFailureReason);
    }

    private static string BuildSummary(Ticket ticket, string? pullRequestUrl) => ticket.Status switch
    {
        TicketStatus.Pending when ticket.StartedAt is not null => "Ticket is pending retry after an earlier execution attempt.",
        TicketStatus.Pending => "Ticket is pending and has not started agent execution.",
        TicketStatus.Running => "Ticket is running agent execution.",
        TicketStatus.WaitingApproval => "Ticket is waiting for approval.",
        TicketStatus.Completed when pullRequestUrl is not null => "Ticket completed with a pull request.",
        TicketStatus.Completed => "Ticket completed without a pull request.",
        TicketStatus.Failed => FailedSummary,
        TicketStatus.Cancelled => CancelledSummary,
        _ => $"Ticket status is {FormatStatus(ticket.Status)}."
    };

    private static string? GetPublicFailureReason(TicketStatus status) => status switch
    {
        TicketStatus.Failed => FailedSummary,
        TicketStatus.Cancelled => CancelledSummary,
        _ => null
    };

    private static string FormatStatus(TicketStatus status) => status.ToString();

    private static string? FormatIssueTracker(IssueTrackerProvider? provider) =>
        provider is null or IssueTrackerProvider.None ? null : provider.Value.ToString();

    private static string? NormalizeExternalUrl(string? value, Func<string, bool> isHostAllowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !isHostAllowed(uri.IdnHost)
            || HasCredentialQuery(uri.Query))
        {
            return null;
        }

        return candidate;
    }

    private static bool HasCredentialQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var encodedName = separator < 0 ? pair : pair[..separator];
            if (HasInvalidPercentEncoding(encodedName))
            {
                return true;
            }

            string name;
            try
            {
                name = Uri.UnescapeDataString(encodedName)
                    .Replace("-", string.Empty, StringComparison.Ordinal)
                    .Replace("_", string.Empty, StringComparison.Ordinal);
            }
            catch (UriFormatException)
            {
                return true;
            }

            if (HasInvalidPercentEncoding(name))
            {
                return true;
            }

            if (CredentialQueryNames.Contains(name)
                || CredentialQueryMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasInvalidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return true;
            }

            index += 2;
        }

        return false;
    }

    private static string? ExtractRepositoryHost(string? repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        var candidate = repositoryUrl.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.IdnHost;
        }

        var at = candidate.IndexOf('@');
        var colon = candidate.IndexOf(':', at + 1);
        return at >= 0 && colon > at + 1 ? candidate[(at + 1)..colon] : null;
    }
}
