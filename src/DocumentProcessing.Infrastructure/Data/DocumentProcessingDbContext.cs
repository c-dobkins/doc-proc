using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Data;

/// <summary>
/// EF Core database context for the document processing platform.
/// </summary>
public class DocumentProcessingDbContext(DbContextOptions<DocumentProcessingDbContext> options)
    : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ProcessingResult> ProcessingResults => Set<ProcessingResult>();
    public DbSet<ExtractionTemplate> ExtractionTemplates => Set<ExtractionTemplate>();
    public DbSet<DocumentField> DocumentFields => Set<DocumentField>();
    public DbSet<CorrectionLog> CorrectionLogs => Set<CorrectionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentProcessingDbContext).Assembly);
    }
}
