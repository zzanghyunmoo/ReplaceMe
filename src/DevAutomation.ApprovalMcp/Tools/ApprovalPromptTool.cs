using System.ComponentModel;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Services;
using DevAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace DevAutomation.ApprovalMcp.Tools;

[McpServerToolType]
public static class ApprovalPromptTool
{
    [McpServerTool(Name = "approval_prompt"), Description("Requests human approval through Slack before Claude Code performs a sensitive operation.")]
    public static async Task<string> ApprovalPromptAsync(
        ApprovalService approvalService,
        DevAutomationDbContext dbContext,
        ITicketNotifier ticketNotifier,
        [Description("Ticket id that is currently being executed. Defaults to the TICKET_ID environment variable.")] Guid? ticketId = null,
        [Description("Claude Code tool or operation name requiring approval.")] string? toolName = null,
        [Description("Claude Code tool or operation name requiring approval, when sent as snake_case.")] string? tool_name = null,
        [Description("JSON input parameters for the requested tool.")] string? inputJson = null,
        [Description("Raw input parameters for the requested tool.")] JsonElement? input = null,
        [Description("Human-readable summary of the requested sensitive operation.")] string? summary = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTicketId = ticketId ?? ResolveTicketIdFromEnvironment();
        var resolvedToolName = string.IsNullOrWhiteSpace(toolName) ? tool_name : toolName;
        var resolvedInputJson = string.IsNullOrWhiteSpace(inputJson) ? input?.GetRawText() ?? "{}" : inputJson;
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == resolvedTicketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket {resolvedTicketId} was not found.");

        if (ticket.Status == DevAutomation.Core.Entities.TicketStatus.Running)
        {
            ticket.MarkWaitingApproval();
            await dbContext.SaveChangesAsync(cancellationToken);
            await ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);
        }

        var notification = new ApprovalNotification(
            ticket.Id,
            ticket.Title,
            string.IsNullOrWhiteSpace(resolvedToolName) ? "unknown" : resolvedToolName,
            string.IsNullOrWhiteSpace(summary) ? resolvedInputJson : summary,
            resolvedInputJson);

        var decision = await approvalService.RequestApprovalAsync(notification, cancellationToken);

        if (ticket.Status == DevAutomation.Core.Entities.TicketStatus.WaitingApproval)
        {
            ticket.MarkRunning(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ticketNotifier.NotifyStatusChangedAsync(ticket, cancellationToken);
        }

        var result = decision.Behavior == ApprovalBehaviors.Allow
            ? new Dictionary<string, object?> { ["behavior"] = ApprovalBehaviors.Allow, ["updatedInput"] = decision.UpdatedInput }
            : new Dictionary<string, object?> { ["behavior"] = ApprovalBehaviors.Deny, ["message"] = decision.Message ?? "Denied." };

        return JsonSerializer.Serialize(result);
    }

    private static Guid ResolveTicketIdFromEnvironment()
    {
        var rawTicketId = Environment.GetEnvironmentVariable("TICKET_ID");
        if (Guid.TryParse(rawTicketId, out var ticketId))
        {
            return ticketId;
        }

        throw new InvalidOperationException("approval_prompt requires ticketId or TICKET_ID.");
    }
}
