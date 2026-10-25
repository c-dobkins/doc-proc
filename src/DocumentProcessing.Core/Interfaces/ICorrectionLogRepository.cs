using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Enqueues correction log entries for persistence within the current unit of work.
/// </summary>
public interface ICorrectionLogRepository
{
    /// <summary>
    /// Stages a correction log entry for insertion.
    /// The entry is committed when the ambient SaveChanges is called
    /// (typically by <see cref="IDocumentRepository.UpdateAsync"/>).
    /// </summary>
    void Add(CorrectionLog log);
}
