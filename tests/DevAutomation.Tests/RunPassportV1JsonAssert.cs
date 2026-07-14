using System.Text.Json;

namespace DevAutomation.Tests;

internal static class RunPassportV1JsonAssert
{
    public static void IsPendingLinearPassport(JsonElement actual, Guid ticketId, DateTimeOffset createdAt)
    {
        var expected = JsonSerializer.SerializeToElement(
            new
            {
                ContractVersion = "run-passport-summary/v1",
                RunPassportId = $"ticket:{ticketId}",
                RunPassportUrl = $"/api/tickets/{ticketId}/run-passport",
                TicketId = ticketId,
                Title = "Build feature",
                Status = "Pending",
                Summary = "Ticket is pending and has not started agent execution.",
                CreatedAt = createdAt,
                StartedAt = (DateTimeOffset?)null,
                CompletedAt = (DateTimeOffset?)null,
                LastLifecycleAt = createdAt,
                IssueTracker = "Linear",
                ExternalIssueKey = "ZZA-56",
                ExternalIssueUrl = "https://linear.app/example/issue/ZZA-56",
                PullRequestUrl = (string?)null,
                NotionDocumentId = (string?)null,
                NotionDocumentUrl = (string?)null,
                TestSummary = (string?)null,
                ResidualRiskSummary = (string?)null,
                FailureReason = (string?)null
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(
            JsonElement.DeepEquals(expected, actual),
            $"Expected the complete v1 payload {expected.GetRawText()}, but received {actual.GetRawText()}.");
    }
}
