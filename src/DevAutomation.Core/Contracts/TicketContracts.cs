using DevAutomation.Core.Entities;

namespace DevAutomation.Core.Contracts;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    string RepoUrl,
    string? BaseBranch,
    bool CreateExternalIssue = false,
    string? ExternalIssueId = null,
    string? ExternalIssueKey = null,
    string? ExternalIssueUrl = null);

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    string RepoUrl,
    string BaseBranch,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? PrUrl,
    string? FailReason,
    IssueTrackerProvider? IssueTracker,
    string? ExternalIssueId,
    string? ExternalIssueKey,
    string? ExternalIssueUrl)
{
    public static TicketResponse From(Ticket ticket) => new(
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.RepoUrl,
        ticket.BaseBranch,
        ticket.Status,
        ticket.CreatedAt,
        ticket.StartedAt,
        ticket.CompletedAt,
        ticket.PrUrl,
        ticket.FailReason,
        ticket.IssueTracker,
        ticket.ExternalIssueId,
        ticket.ExternalIssueKey,
        ticket.ExternalIssueUrl);
}

public sealed record ExecutionLogResponse(Guid Id, Guid TicketId, DateTimeOffset Timestamp, string EventType, string Content)
{
    public static ExecutionLogResponse From(ExecutionLog log) => new(log.Id, log.TicketId, log.Timestamp, log.EventType, log.Content);
}

public sealed record RejectApprovalRequest(string? Reason = null);

public sealed record ApprovalRequestResponse(
    Guid Id,
    Guid TicketId,
    string ToolName,
    string InputJson,
    ApprovalStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? RespondedAt,
    string? ResponderSlackId,
    string? SlackMessageTs,
    string? ResponseReason)
{
    public static ApprovalRequestResponse From(ApprovalRequest request) => new(
        request.Id,
        request.TicketId,
        request.ToolName,
        request.InputJson,
        request.Status,
        request.RequestedAt,
        request.RespondedAt,
        request.ResponderSlackId,
        request.SlackMessageTs,
        request.ResponseReason);
}
