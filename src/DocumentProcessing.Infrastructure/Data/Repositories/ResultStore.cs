using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Data.Repositories;

public class ResultStore(DocumentProcessingDbContext dbContext) : IResultStore
{
    public async Task<ProcessingResult> SaveResultAsync(ProcessingResult result, CancellationToken ct)
    {
        // Remove any existing result for this document (reprocessing scenario).
        // ExecuteDeleteAsync translates to a single DELETE WHERE without loading entities.
        await dbContext.ProcessingResults
            .Where(r => r.DocumentId == result.DocumentId)
            .ExecuteDeleteAsync(ct);

        dbContext.ProcessingResults.Add(result);
        await dbContext.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ProcessingResult?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct)
    {
        return await dbContext.ProcessingResults
            .Include(r => r.ExtractedFields)
            .FirstOrDefaultAsync(r => r.DocumentId == documentId, ct);
    }
}
