using System.Text.Json;
using DevAutomation.Core.Contracts;
using DevAutomation.Core.Entities;

namespace DevAutomation.Tests;

public sealed class RunPassportContractTests
{
    [Fact]
    public void Pristine_pending_ticket_maps_to_v1_ticket_scoped_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", "main", createdAt);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("run-passport-summary/v1", passport.ContractVersion);
        Assert.Equal($"ticket:{ticket.Id}", passport.RunPassportId);
        Assert.Equal($"/api/tickets/{ticket.Id}/run-passport", passport.RunPassportUrl);
        Assert.Equal(ticket.Id, passport.TicketId);
        Assert.Equal("Build feature", passport.Title);
        Assert.Equal("Pending", passport.Status);
        Assert.Equal("Ticket is pending and has not started agent execution.", passport.Summary);
        Assert.Equal(createdAt, passport.CreatedAt);
        Assert.Null(passport.StartedAt);
        Assert.Null(passport.CompletedAt);
        Assert.Equal(createdAt, passport.LastLifecycleAt);
        Assert.Null(passport.PullRequestUrl);
        Assert.Null(passport.NotionDocumentId);
        Assert.Null(passport.NotionDocumentUrl);
        Assert.Null(passport.TestSummary);
        Assert.Null(passport.ResidualRiskSummary);
        Assert.Null(passport.FailureReason);
    }

    [Fact]
    public void Pending_ticket_with_started_at_maps_to_retry_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var startedAt = createdAt.AddMinutes(1);
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, createdAt);
        ticket.MarkRunning(startedAt);
        ticket.MarkPendingRetry();

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Pending", passport.Status);
        Assert.Equal("Ticket is pending retry after an earlier execution attempt.", passport.Summary);
        Assert.Equal(startedAt, passport.StartedAt);
        Assert.Equal(startedAt, passport.LastLifecycleAt);
    }

    [Fact]
    public void Running_ticket_has_explicit_summary()
    {
        var ticket = CreateRunningTicket();

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Running", passport.Status);
        Assert.Equal("Ticket is running agent execution.", passport.Summary);
    }

    [Fact]
    public void Waiting_approval_ticket_has_explicit_summary()
    {
        var ticket = CreateRunningTicket();
        ticket.MarkWaitingApproval();

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("WaitingApproval", passport.Status);
        Assert.Equal("Ticket is waiting for approval.", passport.Summary);
    }

    [Fact]
    public void Linear_issue_reference_maps_only_an_allowed_https_link()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.AttachIssueTracker(IssueTrackerProvider.Linear, "issue-id", "ZZA-56", " https://linear.app/example/issue/ZZA-56 ");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Linear", passport.IssueTracker);
        Assert.Equal("ZZA-56", passport.ExternalIssueKey);
        Assert.Equal("https://linear.app/example/issue/ZZA-56", passport.ExternalIssueUrl);
    }

    [Fact]
    public void Projection_policy_allows_a_configured_jira_host()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.AttachIssueTracker(IssueTrackerProvider.Jira, "10001", "DEV-1", "https://jira.example.com/browse/DEV-1");
        var policy = new RunPassportProjectionPolicy(["https://jira.example.com"]);

        var passport = RunPassportSummaryResponse.From(ticket, policy);

        Assert.Equal("Jira", passport.IssueTracker);
        Assert.Equal("https://jira.example.com/browse/DEV-1", passport.ExternalIssueUrl);
    }

    [Theory]
    [InlineData("http://linear.app/example/issue/ZZA-56")]
    [InlineData("https://user:password@linear.app/example/issue/ZZA-56")]
    [InlineData("https://linear.app/example/issue/ZZA-56?token=secret")]
    [InlineData("https://linear.app/example/issue/ZZA-56?client_secret=secret")]
    [InlineData("https://linear.app/example/issue/ZZA-56?X-Amz-Signature=secret")]
    [InlineData("https://linear.app/example/issue/ZZA-56?foo=token%3Dsecret")]
    [InlineData("https://linear.app/example/issue/ZZA-56?metadata=%7B%22token%22%3A%22secret%22%7D")]
    [InlineData("https://linear.app/example/issue/ZZA-56#access_token=secret")]
    [InlineData("https://linear.app/example/issue/ZZA-56?foo=%ZZ")]
    [InlineData("https://linear.app/example/issue/ZZA-56#view=%ZZ")]
    [InlineData("https://evil.example/example/issue/ZZA-56")]
    public void Unsafe_issue_links_map_to_null(string externalIssueUrl)
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.AttachIssueTracker(IssueTrackerProvider.Linear, "issue-id", "ZZA-56", externalIssueUrl);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Null(passport.ExternalIssueUrl);
    }

    [Fact]
    public void Benign_issue_query_value_and_fragment_are_preserved()
    {
        var url = "https://linear.app/example/issue/ZZA-56?label=security-review#discussion";
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.UtcNow);
        ticket.AttachIssueTracker(IssueTrackerProvider.Linear, "issue-id", "ZZA-56", url);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal(url, passport.ExternalIssueUrl);
    }

    [Fact]
    public void Completed_ticket_maps_a_valid_pull_request_and_completion_summary()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var startedAt = createdAt.AddMinutes(1);
        var completedAt = createdAt.AddMinutes(5);
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, createdAt);
        ticket.MarkRunning(startedAt);
        ticket.MarkCompleted(completedAt, " https://github.com/example/repo/pull/56 ");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Completed", passport.Status);
        Assert.Equal("Ticket completed with a pull request.", passport.Summary);
        Assert.Equal("https://github.com/example/repo/pull/56", passport.PullRequestUrl);
        Assert.Equal(startedAt, passport.StartedAt);
        Assert.Equal(completedAt, passport.CompletedAt);
        Assert.Equal(completedAt, passport.LastLifecycleAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://github.com/example/repo/pull/56")]
    [InlineData("https://user:password@github.com/example/repo/pull/56")]
    [InlineData("https://github.com/example/repo/pull/56?access_token=secret")]
    [InlineData("https://github.com/example/repo/pull/56?foo=token%3Dsecret")]
    [InlineData("https://github.com/example/repo/pull/56?metadata=%7B%22token%22%3A%22secret%22%7D")]
    [InlineData("https://github.com/example/repo/pull/56#access_token=secret")]
    [InlineData("https://github.com/example/repo/pull/56?foo=%ZZ")]
    [InlineData("https://github.com/example/repo/pull/56#view=%ZZ")]
    [InlineData("https://gitlab.com/example/repo/-/merge_requests/56")]
    public void Missing_or_unsafe_pull_request_links_map_to_null(string? pullRequestUrl)
    {
        var ticket = CreateRunningTicket();
        ticket.MarkCompleted(DateTimeOffset.UtcNow, pullRequestUrl);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Null(passport.PullRequestUrl);
        Assert.Equal("Ticket completed without a pull request.", passport.Summary);
    }

    [Fact]
    public void Benign_pull_request_query_value_and_fragment_are_preserved()
    {
        var url = "https://github.com/example/repo/pull/56?view=files#discussion_r1";
        var ticket = CreateRunningTicket();
        ticket.MarkCompleted(DateTimeOffset.UtcNow, url);

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal(url, passport.PullRequestUrl);
        Assert.Equal("Ticket completed with a pull request.", passport.Summary);
    }

    [Fact]
    public void Failed_ticket_exposes_only_a_generic_public_safe_reason()
    {
        var completedAt = DateTimeOffset.Parse("2026-07-09T00:03:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, completedAt.AddMinutes(-3));
        ticket.MarkFailed(completedAt, "token=abc123 at /Users/me/project/file.cs");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Failed", passport.Status);
        Assert.Equal("Execution failed.", passport.Summary);
        Assert.Equal("Execution failed.", passport.FailureReason);
        Assert.Equal(completedAt, passport.LastLifecycleAt);
        var json = JsonSerializer.Serialize(passport);
        Assert.DoesNotContain("abc123", json);
        Assert.DoesNotContain("/Users/me", json);
    }

    [Fact]
    public void Cancelled_ticket_exposes_only_a_generic_public_safe_reason()
    {
        var ticket = CreateRunningTicket();
        ticket.MarkCancelled(DateTimeOffset.UtcNow, "password=secret at C:\\Users\\me\\repo");

        var passport = RunPassportSummaryResponse.From(ticket);

        Assert.Equal("Cancelled", passport.Status);
        Assert.Equal("Execution cancelled.", passport.Summary);
        Assert.Equal("Execution cancelled.", passport.FailureReason);
        var json = JsonSerializer.Serialize(passport);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("C:\\\\Users", json);
    }

    [Fact]
    public void Web_json_contract_uses_v1_names_strings_and_explicit_nulls()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, DateTimeOffset.Parse("2026-07-09T00:00:00Z"));
        ticket.AttachIssueTracker(IssueTrackerProvider.Linear, "issue-id", "ZZA-56", "https://linear.app/example/issue/ZZA-56");
        var passport = RunPassportSummaryResponse.From(ticket);

        var root = JsonSerializer.SerializeToElement(
            passport,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        RunPassportV1JsonAssert.IsPendingLinearPassport(root, ticket.Id, ticket.CreatedAt);
    }

    private static Ticket CreateRunningTicket()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-09T00:00:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://github.com/example/repo.git", null, createdAt);
        ticket.MarkRunning(createdAt.AddMinutes(1));
        return ticket;
    }
}
