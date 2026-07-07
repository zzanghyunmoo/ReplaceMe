using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;

namespace DevAutomation.Core.Abstractions;

public sealed record DocumentReference(DocumentToolProvider Provider, string Id, string Url);

public interface IDocumentToolClient
{
    DocumentToolProvider Provider { get; }

    Task<DocumentReference> CreateTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken);
}

public interface IDocumentToolService
{
    DocumentToolProvider Provider { get; }

    Task<DocumentReference> CreateTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken);
}
