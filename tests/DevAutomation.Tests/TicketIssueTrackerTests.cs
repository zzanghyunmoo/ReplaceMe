using DevAutomation.Core.Entities;

namespace DevAutomation.Tests;

public sealed class TicketIssueTrackerTests
{
    [Fact]
    public void Ticket_can_attach_external_issue_reference()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", null, DateTimeOffset.UtcNow);

        ticket.AttachIssueTracker(IssueTrackerProvider.Jira, " 10001 ", " DEV-42 ", " https://example.atlassian.net/browse/DEV-42 ");

        Assert.True(ticket.HasIssueTrackerReference);
        Assert.Equal(IssueTrackerProvider.Jira, ticket.IssueTracker);
        Assert.Equal("10001", ticket.ExternalIssueId);
        Assert.Equal("DEV-42", ticket.ExternalIssueKey);
        Assert.Equal("https://example.atlassian.net/browse/DEV-42", ticket.ExternalIssueUrl);
    }

    [Fact]
    public void Ticket_rejects_none_issue_tracker_provider()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", null, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => ticket.AttachIssueTracker(IssueTrackerProvider.None, null, null, null));
    }
}
