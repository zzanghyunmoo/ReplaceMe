using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Slack;

public sealed class SlackApprovalNotifier : IApprovalNotifier, ITicketNotifier
{
    private readonly HttpClient _httpClient;
    private readonly SlackOptions _options;
    private readonly ILogger<SlackApprovalNotifier> _logger;

    public SlackApprovalNotifier(HttpClient httpClient, IOptions<SlackOptions> options, ILogger<SlackApprovalNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri(_options.ApiBaseUrl);
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);
        }
    }

    public async Task<SlackMessageRef> SendApprovalRequestAsync(ApprovalRequest approvalRequest, ApprovalNotification notification, CancellationToken cancellationToken)
    {
        var payload = new
        {
            channel = _options.ChannelId,
            text = $"Approval required for {notification.TicketTitle}",
            blocks = BuildApprovalBlocks(approvalRequest, notification)
        };

        var response = await PostSlackAsync("chat.postMessage", payload, cancellationToken);
        var ts = response.GetProperty("ts").GetString() ?? string.Empty;
        return new SlackMessageRef(_options.ChannelId, ts);
    }

    public async Task UpdateApprovalResultAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(approvalRequest.SlackMessageTs))
        {
            return;
        }

        var payload = new
        {
            channel = _options.ChannelId,
            ts = approvalRequest.SlackMessageTs,
            text = $"Approval {approvalRequest.Status}",
            blocks = new object[]
            {
                new { type = "section", text = new { type = "mrkdwn", text = $"*Approval result:* `{approvalRequest.Status}`" } },
                new { type = "context", elements = new object[] { new { type = "mrkdwn", text = $"Request `{approvalRequest.Id}` responded at {approvalRequest.RespondedAt:O} by `{approvalRequest.ResponderSlackId ?? "system"}`" } } }
            }
        };

        await PostSlackAsync("chat.update", payload, cancellationToken);
    }

    public async Task NotifyStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelId) || string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogInformation("Slack notification skipped for ticket {TicketId}; Slack is not configured.", ticket.Id);
            return;
        }

        var pr = string.IsNullOrWhiteSpace(ticket.PrUrl) ? string.Empty : $"\nPR: {ticket.PrUrl}";
        var reason = string.IsNullOrWhiteSpace(ticket.FailReason) ? string.Empty : $"\nReason: {ticket.FailReason}";
        var payload = new
        {
            channel = _options.ChannelId,
            text = $"Ticket {ticket.Status}: {ticket.Title}{pr}{reason}"
        };

        await PostSlackAsync("chat.postMessage", payload, cancellationToken);
    }

    private static object[] BuildApprovalBlocks(ApprovalRequest approvalRequest, ApprovalNotification notification)
    {
        return
        [
            new { type = "section", text = new { type = "mrkdwn", text = $"*승인 요청*\nTicket: `{notification.TicketTitle}`\nTool: `{notification.ToolName}`" } },
            new { type = "section", text = new { type = "mrkdwn", text = $"*작업 요약*\n{notification.Summary}" } },
            new
            {
                type = "actions",
                elements = new object[]
                {
                    new { type = "button", text = new { type = "plain_text", text = "승인" }, style = "primary", action_id = SlackApprovalActionIds.Approve, value = approvalRequest.Id.ToString() },
                    new { type = "button", text = new { type = "plain_text", text = "거절" }, style = "danger", action_id = SlackApprovalActionIds.Reject, value = approvalRequest.Id.ToString() }
                }
            }
        ];
    }

    private async Task<JsonElement> PostSlackAsync(string method, object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.ChannelId))
        {
            _logger.LogWarning("Slack API call {Method} skipped because Slack is not configured.", method);
            using var skippedDocument = JsonDocument.Parse("{\"ok\":true,\"ts\":\"not-configured\"}");
            return skippedDocument.RootElement.Clone();
        }

        using var response = await _httpClient.PostAsJsonAsync(method, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var error = document.RootElement.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : "unknown_error";
            throw new InvalidOperationException($"Slack API {method} failed: {error}");
        }

        return document.RootElement.Clone();
    }
}
