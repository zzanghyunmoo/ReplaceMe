using DevAutomation.Core.Contracts;
using DevAutomation.Core.Entities;

namespace DevAutomation.Tests;

public sealed class RunPassportContractTests
{
    [Fact]
    public void Pending_ticket_maps_to_minimal_run_passport_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", "main", createdAt);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("run-passport-summary/v0", passport.ContractVersion);
        Assert.Equal($"ticket:{ticket.Id}", passport.RunPassportId);
        Assert.Equal($"/api/tickets/{ticket.Id}/run-passport", passport.RunPassportUrl);
        Assert.Equal(ticket.Id, passport.TicketId);
        Assert.Equal("Build feature", passport.Title);
        Assert.Equal("Pending", passport.Status);
        Assert.Equal("Ticket is pending and has not started agent execution.", passport.Summary);
        Assert.Equal(createdAt, passport.CreatedAt);
        Assert.Null(passport.StartedAt);
        Assert.Null(passport.CompletedAt);
        Assert.Equal(createdAt, passport.UpdatedAt);
        Assert.Null(passport.PullRequestUrl);
        Assert.Null(passport.NotionDocumentId);
        Assert.Null(passport.NotionDocumentUrl);
        Assert.Null(passport.TestSummary);
        Assert.Null(passport.ResidualRiskSummary);
        Assert.Null(passport.FailureReason);
    }

    [Fact]
    public void Ticket_issue_tracker_reference_maps_to_source_link_fields()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.AttachIssueTracker(IssueTrackerProvider.Linear, "issue-id", "ZZA-56", "https://linear.app/example/issue/ZZA-56");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Linear", passport.IssueTracker);
        Assert.Equal("ZZA-56", passport.ExternalIssueKey);
        Assert.Equal("https://linear.app/example/issue/ZZA-56", passport.ExternalIssueUrl);
    }

    [Fact]
    public void Completed_ticket_maps_pull_request_and_completion_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var startedAt = createdAt.AddMinutes(1);
        var completedAt = createdAt.AddMinutes(5);
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, createdAt);
        ticket.MarkRunning(startedAt);
        ticket.MarkCompleted(completedAt, "https://github.com/example/repo/pull/56");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Completed", passport.Status);
        Assert.Equal("Ticket completed with a pull request.", passport.Summary);
        Assert.Equal("https://github.com/example/repo/pull/56", passport.PullRequestUrl);
        Assert.Equal(startedAt, passport.StartedAt);
        Assert.Equal(completedAt, passport.CompletedAt);
        Assert.Equal(completedAt, passport.UpdatedAt);
    }

    [Fact]
    public void Failed_ticket_maps_failure_reason_and_failure_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var completedAt = createdAt.AddMinutes(3);
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, createdAt);
        ticket.MarkFailed(completedAt, "Agent failed.");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Failed", passport.Status);
        Assert.Equal("Ticket failed: Agent failed.", passport.Summary);
        Assert.Equal("Agent failed.", passport.FailureReason);
        Assert.Equal(completedAt, passport.CompletedAt);
        Assert.Equal(completedAt, passport.UpdatedAt);
    }

    [Fact]
    public void Failure_reason_redacts_secret_assignments_and_local_paths()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.MarkFailed(DateTimeOffset.UtcNow, "Agent failed with token=abc123 at /Users/me/project/file.cs");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Agent failed with token=[REDACTED] at [LOCAL_PATH]", passport.FailureReason);
        Assert.Equal("Ticket failed: Agent failed with token=[REDACTED] at [LOCAL_PATH].", passport.Summary);
    }
}
