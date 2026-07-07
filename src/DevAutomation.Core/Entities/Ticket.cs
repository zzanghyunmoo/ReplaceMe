namespace DevAutomation.Core.Entities;

public sealed class Ticket
{
    private readonly List<ApprovalRequest> _approvalRequests = [];
    private readonly List<ExecutionLog> _executionLogs = [];

    private Ticket() { }

    private Ticket(string title, string description, string repoUrl, string baseBranch, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        RepoUrl = repoUrl;
        BaseBranch = baseBranch;
        Status = TicketStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string RepoUrl { get; private set; } = string.Empty;
    public string BaseBranch { get; private set; } = "main";
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? PrUrl { get; private set; }
    public string? FailReason { get; private set; }
    public string? ContainerId { get; private set; }

    public IReadOnlyCollection<ApprovalRequest> ApprovalRequests => _approvalRequests;
    public IReadOnlyCollection<ExecutionLog> ExecutionLogs => _executionLogs;

    public static Ticket Create(string title, string description, string repoUrl, string? baseBranch, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(repoUrl)) throw new ArgumentException("RepoUrl is required.", nameof(repoUrl));

        return new Ticket(title.Trim(), description.Trim(), repoUrl.Trim(), string.IsNullOrWhiteSpace(baseBranch) ? "main" : baseBranch.Trim(), createdAt);
    }

    public void MarkRunning(DateTimeOffset startedAt)
    {
        EnsureStatus(TicketStatus.Pending, TicketStatus.WaitingApproval);
        Status = TicketStatus.Running;
        StartedAt ??= startedAt;
        FailReason = null;
    }

    public void MarkWaitingApproval()
    {
        EnsureStatus(TicketStatus.Running);
        Status = TicketStatus.WaitingApproval;
    }

    public void MarkCompleted(DateTimeOffset completedAt, string? prUrl)
    {
        EnsureStatus(TicketStatus.Running, TicketStatus.WaitingApproval);
        Status = TicketStatus.Completed;
        CompletedAt = completedAt;
        PrUrl = prUrl;
        FailReason = null;
    }

    public void MarkFailed(DateTimeOffset completedAt, string reason)
    {
        if (Status is TicketStatus.Completed or TicketStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot fail a {Status} ticket.");
        }

        Status = TicketStatus.Failed;
        CompletedAt = completedAt;
        FailReason = string.IsNullOrWhiteSpace(reason) ? "Unknown failure" : reason;
    }

    public void MarkCancelled(DateTimeOffset completedAt, string? reason = null)
    {
        if (Status is TicketStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed ticket.");
        }

        Status = TicketStatus.Cancelled;
        CompletedAt = completedAt;
        FailReason = reason;
    }

    public void AttachContainer(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId)) throw new ArgumentException("Container id is required.", nameof(containerId));
        ContainerId = containerId;
    }

    public void ClearContainer() => ContainerId = null;

    private void EnsureStatus(params TicketStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot transition ticket {Id} from {Status}. Allowed: {string.Join(", ", allowed)}.");
        }
    }
}
