using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Data.Repositories;

public class TemplateRepository(DocumentProcessingDbContext dbContext) : ITemplateRepository
{
    public async Task<ExtractionTemplate> AddAsync(ExtractionTemplate template, CancellationToken ct)
    {
        dbContext.ExtractionTemplates.Add(template);
        await dbContext.SaveChangesAsync(ct);
        return template;
    }

    public async Task<ExtractionTemplate?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await dbContext.ExtractionTemplates
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<ExtractionTemplate>> GetAllAsync(CancellationToken ct)
    {
        return await dbContext.ExtractionTemplates
            .Include(t => t.Fields)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<ExtractionTemplate> UpdateAsync(ExtractionTemplate template, CancellationToken ct)
    {
        template.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ExtractionTemplates.Update(template);
        await dbContext.SaveChangesAsync(ct);
        return template;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var template = await dbContext.ExtractionTemplates.FindAsync([id], ct);
        if (template is not null)
        {
            dbContext.ExtractionTemplates.Remove(template);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
