using System.Text.RegularExpressions;
using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Contracts;

public sealed partial record RunPassportSummaryResponse(
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
    DateTimeOffset UpdatedAt,
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
    public const string CurrentContractVersion = "run-passport-summary/v0";

    public static RunPassportSummaryResponse From(Ticket ticket)
    {
        var updatedAt = ticket.CompletedAt ?? ticket.StartedAt ?? ticket.CreatedAt;
        var sanitizedFailureReason = SanitizeFailureReason(ticket.FailReason);

        return new RunPassportSummaryResponse(
            ContractVersion: CurrentContractVersion,
            RunPassportId: $"ticket:{ticket.Id}",
            RunPassportUrl: $"/api/tickets/{ticket.Id}/run-passport",
            TicketId: ticket.Id,
            Title: ticket.Title,
            Status: FormatStatus(ticket.Status),
            Summary: BuildSummary(ticket, sanitizedFailureReason),
            CreatedAt: ticket.CreatedAt,
            StartedAt: ticket.StartedAt,
            CompletedAt: ticket.CompletedAt,
            UpdatedAt: updatedAt,
            IssueTracker: FormatIssueTracker(ticket.IssueTracker),
            ExternalIssueKey: ticket.ExternalIssueKey,
            ExternalIssueUrl: ticket.ExternalIssueUrl,
            PullRequestUrl: ticket.PrUrl,
            NotionDocumentId: null,
            NotionDocumentUrl: null,
            TestSummary: null,
            ResidualRiskSummary: null,
            FailureReason: sanitizedFailureReason);
    }

    private static string BuildSummary(Ticket ticket, string? sanitizedFailureReason) => ticket.Status switch
    {
        TicketStatus.Pending => "Ticket is pending and has not started agent execution.",
        TicketStatus.Running => "Ticket is running agent execution.",
        TicketStatus.WaitingApproval => "Ticket is waiting for approval.",
        TicketStatus.Completed when !string.IsNullOrWhiteSpace(ticket.PrUrl) => "Ticket completed with a pull request.",
        TicketStatus.Completed => "Ticket completed without a pull request.",
        TicketStatus.Failed => $"Ticket failed: {NormalizeSentence(sanitizedFailureReason ?? "Unknown failure")}",
        TicketStatus.Cancelled => string.IsNullOrWhiteSpace(sanitizedFailureReason)
            ? "Ticket was cancelled."
            : $"Ticket was cancelled: {NormalizeSentence(sanitizedFailureReason)}",
        _ => $"Ticket status is {FormatStatus(ticket.Status)}."
    };

    private static string FormatStatus(TicketStatus status) => status switch
    {
        TicketStatus.Pending => "Pending",
        TicketStatus.Running => "Running",
        TicketStatus.WaitingApproval => "WaitingApproval",
        TicketStatus.Completed => "Completed",
        TicketStatus.Failed => "Failed",
        TicketStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    private static string? FormatIssueTracker(IssueTrackerProvider? provider) => provider switch
    {
        IssueTrackerProvider.Jira => "Jira",
        IssueTrackerProvider.Linear => "Linear",
        IssueTrackerProvider.None => null,
        null => null,
        _ => provider.ToString()
    };

    private static string? SanitizeFailureReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutSecrets = SecretAssignmentRegex().Replace(value.Trim(), "$1=[REDACTED]");
        return LocalPathRegex().Replace(withoutSecrets, "[LOCAL_PATH]");
    }

    private static string NormalizeSentence(string value) => $"{value.Trim().TrimEnd('.')}.";

    [GeneratedRegex(@"(?i)\b(token|secret|password|api[-_]?key)=[^\s]+")]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"(?<!:)/(?:Users|home|private/tmp|tmp|var/folders)/[^\s]+")]
    private static partial Regex LocalPathRegex();
}
