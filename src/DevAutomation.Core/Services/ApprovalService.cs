using System.Text.Json;
using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Core.Services;

public sealed record ApprovalDecision(string Behavior, JsonElement? UpdatedInput, string? Message)
{
    public static ApprovalDecision Allow(JsonElement? updatedInput) => new(ApprovalBehaviors.Allow, updatedInput, null);
    public static ApprovalDecision Deny(string message) => new(ApprovalBehaviors.Deny, null, message);
}

public sealed class ApprovalService
{
    private readonly IApprovalRequestRepository _repository;
    private readonly IApprovalNotifier _notifier;
    private readonly IClock _clock;
    private readonly ApprovalOptions _options;

    public ApprovalService(
        IApprovalRequestRepository repository,
        IApprovalNotifier notifier,
        IClock clock,
        IOptions<ApprovalOptions> options)
    {
        _repository = repository;
        _notifier = notifier;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ApprovalDecision> RequestApprovalAsync(
        ApprovalNotification notification,
        CancellationToken cancellationToken)
    {
        var request = new ApprovalRequest(notification.TicketId, notification.ToolName, notification.InputJson, _clock.UtcNow);
        await _repository.AddAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var slackMessage = await _notifier.SendApprovalRequestAsync(request, notification, cancellationToken);
        request.RecordSlackMessage(slackMessage.MessageTs);
        await _repository.SaveChangesAsync(cancellationToken);

        var deadline = _clock.UtcNow + _options.ApprovalTimeout;
        while (_clock.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var latest = await _repository.GetAsync(request.Id, cancellationToken) ?? request;
            switch (latest.Status)
            {
                case ApprovalStatus.Approved:
                    return ApprovalDecision.Allow(ParseInput(latest.InputJson));
                case ApprovalStatus.Rejected:
                    return ApprovalDecision.Deny(latest.ResponseReason ?? "Rejected by user.");
                case ApprovalStatus.TimedOut:
                    return ApprovalDecision.Deny("Approval request timed out.");
            }

            await Task.Delay(_options.PollInterval, cancellationToken);
        }

        var timedOut = await _repository.MarkTimedOutAsync(request.Id, _clock.UtcNow, cancellationToken);
        if (timedOut?.Status == ApprovalStatus.Approved)
        {
            return ApprovalDecision.Allow(ParseInput(timedOut.InputJson));
        }

        if (timedOut?.Status == ApprovalStatus.Rejected)
        {
            return ApprovalDecision.Deny(timedOut.ResponseReason ?? "Rejected by user.");
        }

        if (timedOut?.Status == ApprovalStatus.TimedOut)
        {
            await _notifier.UpdateApprovalResultAsync(timedOut, cancellationToken);
        }

        return ApprovalDecision.Deny("Approval request timed out.");
    }

    private static JsonElement? ParseInput(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(inputJson);
        return document.RootElement.Clone();
    }
}
