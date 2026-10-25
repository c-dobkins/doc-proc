using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Persists and retrieves processing results and related data.
/// </summary>
public interface IResultStore
{
    Task<ProcessingResult> SaveResultAsync(ProcessingResult result, CancellationToken ct);

    Task<ProcessingResult?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct);
}
