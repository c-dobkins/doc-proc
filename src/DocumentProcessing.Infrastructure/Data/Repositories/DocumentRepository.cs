using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Data.Repositories;

public class DocumentRepository(DocumentProcessingDbContext dbContext) : IDocumentRepository
{
    public async Task<Document> AddAsync(Document document, CancellationToken ct)
    {
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(ct);
        return document;
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await dbContext.Documents
            .Include(d => d.ProcessingResult)
                .ThenInclude(r => r!.ExtractedFields)
            .Include(d => d.Template)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync(int page, int pageSize, DocumentStatus? statusFilter, CancellationToken ct)
    {
        var query = dbContext.Documents.AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(d => d.Status == statusFilter.Value);

        return await query
            .Include(d => d.ProcessingResult)
            .OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<Document> UpdateAsync(Document document, CancellationToken ct)
    {
        dbContext.Documents.Update(document);
        await dbContext.SaveChangesAsync(ct);
        return document;
    }

    public async Task<int> GetCountAsync(DocumentStatus? statusFilter, CancellationToken ct)
    {
        var query = dbContext.Documents.AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(d => d.Status == statusFilter.Value);

        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DocumentStatus, int>> GetStatusCountsAsync(CancellationToken ct)
    {
        var counts = await dbContext.Documents
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }
}
