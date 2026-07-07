using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.IssueTrackers;

public sealed class IssueTrackerService : IIssueTrackerService
{
    private readonly IIssueTrackerClient? _client;
    private readonly ILogger<IssueTrackerService> _logger;

    public IssueTrackerService(IEnumerable<IIssueTrackerClient> clients, IOptions<IssueTrackerOptions> options, ILogger<IssueTrackerService> logger)
    {
        Provider = options.Value.Provider;
        _client = clients.SingleOrDefault();
        _logger = logger;
    }

    public IssueTrackerProvider Provider { get; }

    public Task<ExternalIssueReference> CreateIssueAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (Provider == IssueTrackerProvider.None)
        {
            throw new InvalidOperationException("Issue tracker provider is None.");
        }

        if (_client is null || _client.Provider != Provider)
        {
            throw new InvalidOperationException($"Issue tracker provider '{Provider}' is not registered as the single active provider.");
        }

        return _client.CreateIssueAsync(ticket, cancellationToken);
    }

    public Task NotifyCompletedAsync(Ticket ticket, string? changeRequestUrl, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(changeRequestUrl)
            ? $"DevAutomation completed ticket `{ticket.Id}`."
            : $"DevAutomation completed ticket `{ticket.Id}`. Change request: {changeRequestUrl}";
        return TryAddCommentAsync(ticket, message, cancellationToken);
    }

    public Task NotifyFailedAsync(Ticket ticket, string reason, CancellationToken cancellationToken)
    {
        return TryAddCommentAsync(ticket, $"DevAutomation failed ticket `{ticket.Id}`: {reason}", cancellationToken);
    }

    private async Task TryAddCommentAsync(Ticket ticket, string message, CancellationToken cancellationToken)
    {
        if (!ticket.HasIssueTrackerReference || Provider == IssueTrackerProvider.None)
        {
            return;
        }

        if (_client is null || _client.Provider != Provider)
        {
            _logger.LogWarning("Issue tracker provider {Provider} is not registered; skipping ticket {TicketId} update.", Provider, ticket.Id);
            return;
        }

        try
        {
            await _client.AddCommentAsync(ticket, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update {Provider} issue for ticket {TicketId}; ticket state is preserved.", Provider, ticket.Id);
        }
    }
}
