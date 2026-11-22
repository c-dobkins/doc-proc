using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Manages extraction templates.
/// </summary>
public interface ITemplateRepository
{
    Task<ExtractionTemplate> AddAsync(ExtractionTemplate template, CancellationToken ct);

    Task<ExtractionTemplate?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ExtractionTemplate>> GetAllAsync(CancellationToken ct);

    Task<ExtractionTemplate> UpdateAsync(ExtractionTemplate template, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
