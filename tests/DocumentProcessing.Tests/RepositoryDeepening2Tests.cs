using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Data;
using DocumentProcessing.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Tests;

/// <summary>
/// Tests for repository and store changes made in round 2 architecture deepening.
/// Uses SQLite in-memory to exercise real EF Core behaviour including ExecuteDeleteAsync.
/// </summary>
public class RepositoryDeepening2Tests : IDisposable
{
    private readonly DocumentProcessingDbContext _dbContext;

    public RepositoryDeepening2Tests()
    {
        var options = new DbContextOptionsBuilder<DocumentProcessingDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _dbContext = new DocumentProcessingDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    // ──────────────────────────────────────────────────────────────
    // ICorrectionLogRepository
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CorrectionLogRepository_Add_StagesEntryWithoutSaving()
    {
        var repo = new CorrectionLogRepository(_dbContext);
        var log = new CorrectionLog
        {
            DocumentId = Guid.NewGuid(),
            FieldName = "Amount",
            OriginalValue = "100",
            CorrectedValue = "200"
        };

        repo.Add(log);

        // Staged but not persisted yet
        Assert.Equal(EntityState.Added, _dbContext.Entry(log).State);
        Assert.Equal(0, _dbContext.CorrectionLogs.Count());
    }

    [Fact]
    public async Task CorrectionLogRepository_Add_CommittedBySaveChanges()
    {
        var repo = new CorrectionLogRepository(_dbContext);
        var log = new CorrectionLog
        {
            DocumentId = Guid.NewGuid(),
            FieldName = "InvoiceNumber",
            OriginalValue = "INV-001",
            CorrectedValue = "INV-002"
        };

        repo.Add(log);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await _dbContext.CorrectionLogs.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        var saved = await _dbContext.CorrectionLogs.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("InvoiceNumber", saved.FieldName);
        Assert.Equal("INV-001", saved.OriginalValue);
        Assert.Equal("INV-002", saved.CorrectedValue);
    }

    // ──────────────────────────────────────────────────────────────
    // IDocumentRepository.GetStatusCountsAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsCountPerStatus()
    {
        _dbContext.Documents.AddRange(
            new Document { FileName = "a.pdf", Status = DocumentStatus.Completed },
            new Document { FileName = "b.pdf", Status = DocumentStatus.Completed },
            new Document { FileName = "c.pdf", Status = DocumentStatus.Failed },
            new Document { FileName = "d.pdf", Status = DocumentStatus.NeedsReview }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DocumentRepository(_dbContext);
        var counts = await repo.GetStatusCountsAsync(CancellationToken.None);

        Assert.Equal(2, counts[DocumentStatus.Completed]);
        Assert.Equal(1, counts[DocumentStatus.Failed]);
        Assert.Equal(1, counts[DocumentStatus.NeedsReview]);
    }

    [Fact]
    public async Task GetStatusCountsAsync_EmptyTable_ReturnsEmptyDictionary()
    {
        var repo = new DocumentRepository(_dbContext);
        var counts = await repo.GetStatusCountsAsync(CancellationToken.None);

        Assert.Empty(counts);
    }

    [Fact]
    public async Task GetStatusCountsAsync_UnrepresentedStatuses_Absent()
    {
        _dbContext.Documents.Add(new Document { FileName = "x.pdf", Status = DocumentStatus.Approved });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DocumentRepository(_dbContext);
        var counts = await repo.GetStatusCountsAsync(CancellationToken.None);

        Assert.False(counts.ContainsKey(DocumentStatus.Failed));
        Assert.True(counts.ContainsKey(DocumentStatus.Approved));
    }

    // ──────────────────────────────────────────────────────────────
    // ResultStore — upsert (ExecuteDeleteAsync + Add)
    // ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreateDocumentAsync()
    {
        var doc = new Document { FileName = "test.pdf", Status = DocumentStatus.Processing };
        _dbContext.Documents.Add(doc);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return doc.Id;
    }

    [Fact]
    public async Task ResultStore_SaveResultAsync_InsertsWhenNoExistingResult()
    {
        var docId = await CreateDocumentAsync();
        var result = new ProcessingResult
        {
            DocumentId = docId,
            RawOcrText = "text",
            AiProvider = "Test"
        };

        var store = new ResultStore(_dbContext);
        await store.SaveResultAsync(result, CancellationToken.None);

        Assert.Equal(1, await _dbContext.ProcessingResults.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(docId, (await _dbContext.ProcessingResults.FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).DocumentId);
    }

    [Fact]
    public async Task ResultStore_SaveResultAsync_ReplacesExistingResult()
    {
        var docId = await CreateDocumentAsync();
        var original = new ProcessingResult
        {
            DocumentId = docId,
            RawOcrText = "original",
            AiProvider = "OldProvider",
            ExtractedFields =
            [
                new DocumentField { Name = "Field1", Value = "old", DataType = Core.Enums.FieldDataType.String }
            ]
        };
        _dbContext.ProcessingResults.Add(original);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        var replacement = new ProcessingResult
        {
            DocumentId = docId,
            RawOcrText = "updated",
            AiProvider = "NewProvider",
            ExtractedFields =
            [
                new DocumentField { Name = "Field1", Value = "new", DataType = Core.Enums.FieldDataType.String }
            ]
        };

        var store = new ResultStore(_dbContext);
        await store.SaveResultAsync(replacement, CancellationToken.None);

        Assert.Equal(1, await _dbContext.ProcessingResults.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        var saved = await _dbContext.ProcessingResults.Include(r => r.ExtractedFields).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("NewProvider", saved.AiProvider);
        Assert.Equal("new", saved.ExtractedFields[0].Value);
    }

    [Fact]
    public async Task ResultStore_SaveResultAsync_OldExtractedFieldsRemoved()
    {
        var docId = await CreateDocumentAsync();
        var original = new ProcessingResult
        {
            DocumentId = docId,
            RawOcrText = "original",
            AiProvider = "Provider",
            ExtractedFields =
            [
                new DocumentField { Name = "OldField", Value = "x", DataType = Core.Enums.FieldDataType.String },
                new DocumentField { Name = "OtherField", Value = "y", DataType = Core.Enums.FieldDataType.String }
            ]
        };
        _dbContext.ProcessingResults.Add(original);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        var replacement = new ProcessingResult
        {
            DocumentId = docId,
            RawOcrText = "updated",
            AiProvider = "Provider",
            ExtractedFields =
            [
                new DocumentField { Name = "NewField", Value = "z", DataType = Core.Enums.FieldDataType.String }
            ]
        };

        var store = new ResultStore(_dbContext);
        await store.SaveResultAsync(replacement, CancellationToken.None);

        var fields = await _dbContext.DocumentFields.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(fields);
        Assert.Equal("NewField", fields[0].Name);
    }
}

