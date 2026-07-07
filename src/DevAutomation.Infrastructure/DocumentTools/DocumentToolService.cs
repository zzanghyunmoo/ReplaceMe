using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.DocumentTools;

public sealed class DocumentToolService : IDocumentToolService
{
    private readonly IDocumentToolClient? _client;

    public DocumentToolService(IEnumerable<IDocumentToolClient> clients, IOptions<DocumentToolOptions> options)
    {
        Provider = options.Value.Provider;
        _client = clients.SingleOrDefault();
    }

    public DocumentToolProvider Provider { get; }

    public Task<DocumentReference> CreateTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (Provider == DocumentToolProvider.None)
        {
            throw new InvalidOperationException("Document tool provider is None.");
        }

        if (_client is null || _client.Provider != Provider)
        {
            throw new InvalidOperationException($"Document tool provider '{Provider}' is not registered as the single active provider.");
        }

        return _client.CreateTicketDocumentAsync(ticket, cancellationToken);
    }
}
