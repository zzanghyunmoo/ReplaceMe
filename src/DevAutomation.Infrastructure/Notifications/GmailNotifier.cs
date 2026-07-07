using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Notifications;

public sealed class GmailNotifier : ITicketNotifier, IApprovalNotifier
{
    private readonly HttpClient _httpClient;
    private readonly GmailOptions _gmailOptions;
    private readonly NotifierOptions _notifierOptions;
    private readonly ILogger<GmailNotifier> _logger;

    public GmailNotifier(HttpClient httpClient, IOptions<GmailOptions> gmailOptions, IOptions<NotifierOptions> notifierOptions, ILogger<GmailNotifier> logger)
    {
        _httpClient = httpClient;
        _gmailOptions = gmailOptions.Value;
        _notifierOptions = notifierOptions.Value;
        _logger = logger;
    }

    public Task NotifyStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var subject = $"{ticket.Status}: {ticket.Title}";
        var body = $"""
Ticket: {ticket.Title}
Status: {ticket.Status}
Ticket ID: {ticket.Id}
Repository: {ticket.RepoUrl}
Branch: {ticket.BaseBranch}
PR/MR: {ticket.PrUrl ?? "-"}
Failure: {ticket.FailReason ?? "-"}
""";
        return SendEmailAsync(subject, body, cancellationToken);
    }

    public async Task<SlackMessageRef> SendApprovalRequestAsync(ApprovalRequest approvalRequest, ApprovalNotification notification, CancellationToken cancellationToken)
    {
        var approvalsUrl = $"{_notifierOptions.PublicBaseUrl.TrimEnd('/')}/api/approvals";
        var body = $"""
Approval requested for DevAutomation ticket.

Ticket: {notification.TicketTitle}
Ticket ID: {notification.TicketId}
Tool: {notification.ToolName}
Summary: {notification.Summary}
Approval request ID: {approvalRequest.Id}

Review pending approvals via API: {approvalsUrl}
""";
        var messageId = await SendEmailAsync($"Approval required: {notification.TicketTitle}", body, cancellationToken);
        return new SlackMessageRef(_gmailOptions.ToAddress, messageId ?? "gmail-message");
    }

    public Task UpdateApprovalResultAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
    {
        return SendEmailAsync(
            $"Approval {approvalRequest.Status}: {approvalRequest.ToolName}",
            $"Approval request {approvalRequest.Id} is now {approvalRequest.Status}. Responder: {approvalRequest.ResponderSlackId ?? "system"}",
            cancellationToken);
    }

    private async Task<string?> SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_gmailOptions.AccessToken)
            || string.IsNullOrWhiteSpace(_gmailOptions.FromAddress)
            || string.IsNullOrWhiteSpace(_gmailOptions.ToAddress))
        {
            _logger.LogInformation("Gmail notification skipped because Gmail is not configured.");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildSendUrl());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _gmailOptions.AccessToken);
        request.Content = JsonContent.Create(new { raw = BuildRawMessage(subject, body) });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gmail API failed with {(int)response.StatusCode}: {responseBody}");
        }

        return responseBody;
    }

    private Uri BuildSendUrl()
    {
        var baseUri = new Uri(_gmailOptions.ApiBaseUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, $"users/{Uri.EscapeDataString(_gmailOptions.UserId)}/messages/send");
    }

    private string BuildRawMessage(string subject, string body)
    {
        var safeSubject = $"{_gmailOptions.SubjectPrefix} {subject}".Trim();
        var mime = $"""
From: {_gmailOptions.FromAddress}
To: {_gmailOptions.ToAddress}
Subject: {safeSubject}
Content-Type: text/plain; charset=utf-8

{body}
""";
        return Base64UrlEncode(Encoding.UTF8.GetBytes(mime));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
