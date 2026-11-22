using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Persists and retrieves documents.
/// </summary>
public interface IDocumentRepository
{
    Task<Document> AddAsync(Document document, CancellationToken ct);

    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Document>> GetAllAsync(int page, int pageSize, DocumentStatus? statusFilter, CancellationToken ct);

    Task<Document> UpdateAsync(Document document, CancellationToken ct);

    Task<int> GetCountAsync(DocumentStatus? statusFilter, CancellationToken ct);

    /// <summary>
    /// Returns the count of documents for every status in a single database query.
    /// </summary>
    Task<IReadOnlyDictionary<DocumentStatus, int>> GetStatusCountsAsync(CancellationToken ct);
}
