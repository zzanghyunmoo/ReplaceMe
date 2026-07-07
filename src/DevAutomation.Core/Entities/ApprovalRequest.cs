namespace DevAutomation.Core.Entities;

public sealed class ApprovalRequest
{
    private ApprovalRequest() { }

    public ApprovalRequest(Guid ticketId, string toolName, string inputJson, DateTimeOffset requestedAt)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        ToolName = string.IsNullOrWhiteSpace(toolName) ? "unknown" : toolName.Trim();
        InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson;
        Status = ApprovalStatus.Pending;
        RequestedAt = requestedAt;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string ToolName { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = "{}";
    public ApprovalStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public string? ResponderSlackId { get; private set; }
    public string? SlackMessageTs { get; private set; }
    public string? ResponseReason { get; private set; }
    public Ticket? Ticket { get; private set; }

    public bool IsTerminal => Status is ApprovalStatus.Approved or ApprovalStatus.Rejected or ApprovalStatus.TimedOut;

    public void RecordSlackMessage(string slackMessageTs)
    {
        if (string.IsNullOrWhiteSpace(slackMessageTs)) throw new ArgumentException("Slack message timestamp is required.", nameof(slackMessageTs));
        SlackMessageTs = slackMessageTs;
    }

    public void Approve(string responderSlackId, DateTimeOffset respondedAt)
    {
        EnsurePending();
        Status = ApprovalStatus.Approved;
        ResponderSlackId = responderSlackId;
        RespondedAt = respondedAt;
    }

    public void Reject(string responderSlackId, DateTimeOffset respondedAt, string? reason = null)
    {
        EnsurePending();
        Status = ApprovalStatus.Rejected;
        ResponderSlackId = responderSlackId;
        RespondedAt = respondedAt;
        ResponseReason = reason;
    }

    public void TimeOut(DateTimeOffset respondedAt)
    {
        EnsurePending();
        Status = ApprovalStatus.TimedOut;
        RespondedAt = respondedAt;
        ResponseReason = "Approval timed out.";
    }

    private void EnsurePending()
    {
        if (Status != ApprovalStatus.Pending)
        {
            throw new InvalidOperationException($"Approval request {Id} is already {Status}.");
        }
    }
}
