using DevAutomation.Core.Entities;
using DevAutomation.Core.Services;

namespace DevAutomation.Tests;

public sealed class TicketStateMachineTests
{
    [Fact]
    public void Ticket_can_move_through_running_and_completed_states()
    {
        var now = DateTimeOffset.Parse("2026-07-07T00:00:00Z");
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", "main", now);
        var stateMachine = new TicketStateMachine();

        stateMachine.MarkRunning(ticket, now.AddMinutes(1));
        stateMachine.MarkCompleted(ticket, now.AddMinutes(2), "https://github.test/pull/1");

        Assert.Equal(TicketStatus.Completed, ticket.Status);
        Assert.Equal(now.AddMinutes(1), ticket.StartedAt);
        Assert.Equal(now.AddMinutes(2), ticket.CompletedAt);
        Assert.Equal("https://github.test/pull/1", ticket.PrUrl);
    }

    [Fact]
    public void Ticket_cannot_complete_before_running()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", "main", DateTimeOffset.UtcNow);
        var stateMachine = new TicketStateMachine();

        var error = Assert.Throws<InvalidOperationException>(() => stateMachine.MarkCompleted(ticket, DateTimeOffset.UtcNow, null));

        Assert.Contains("Cannot transition", error.Message);
        Assert.Equal(TicketStatus.Pending, ticket.Status);
    }

    [Fact]
    public void Running_ticket_can_be_cancelled()
    {
        var ticket = Ticket.Create("Build feature", "Do the work", "https://example.test/repo.git", "main", DateTimeOffset.UtcNow);
        var stateMachine = new TicketStateMachine();

        stateMachine.MarkRunning(ticket, DateTimeOffset.UtcNow);
        stateMachine.MarkCancelled(ticket, DateTimeOffset.UtcNow, "User cancelled.");

        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Equal("User cancelled.", ticket.FailReason);
    }
}
