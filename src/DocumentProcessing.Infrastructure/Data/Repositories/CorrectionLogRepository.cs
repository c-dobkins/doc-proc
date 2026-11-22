using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Infrastructure.Data.Repositories;

/// <summary>
/// Stages correction log entries into the EF Core change tracker.
/// Entries are committed when DocumentRepository.UpdateAsync calls SaveChangesAsync.
/// </summary>
public class CorrectionLogRepository(DocumentProcessingDbContext dbContext) : ICorrectionLogRepository
{
    public void Add(CorrectionLog log) => dbContext.CorrectionLogs.Add(log);
}
