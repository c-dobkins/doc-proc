using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Pipeline stage 1: stores an incoming file and creates its <see cref="Document"/> record.
/// Callers validate the request (size, type, signature) and enqueue the document for processing.
/// </summary>
public interface IIngestionHandler
{
    Task<Document> IngestAsync(IngestionRequest request, CancellationToken ct);
}
