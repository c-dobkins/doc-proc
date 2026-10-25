using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Runtime.CompilerServices;

namespace DocumentProcessing.Tests.Database;

/// <summary>
/// Model/migration consistency checks. These need no database.
/// </summary>
public class MigrationModelTests
{
    private static DocumentProcessingDbContext CreateNpgsqlContext() =>
        new(new DbContextOptionsBuilder<DocumentProcessingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    [Fact]
    public void Migrations_CoverCurrentModel_NoPendingModelChanges()
    {
        using var db = CreateNpgsqlContext();

        // Fails when the model changes without a new migration: run
        // dotnet ef migrations add <Name> -p src/DocumentProcessing.Infrastructure -s src/DocumentProcessing.Infrastructure -o Data/Migrations
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void FindMissingColumns_SchemaMatchesModel_ReturnsEmpty()
    {
        using var db = CreateNpgsqlContext();
        var allColumns = ExpectedColumns(db);

        Assert.Empty(DatabaseInitializer.FindMissingColumns(db.Model, allColumns));
        Assert.Contains(("Documents", "StoragePath"), allColumns);
        Assert.Contains(("TemplateField", "Name"), allColumns);
    }

    [Fact]
    public void FindMissingColumns_ColumnAbsent_ReportsIt()
    {
        using var db = CreateNpgsqlContext();
        var columns = ExpectedColumns(db);
        columns.Remove(("Documents", "StoragePath"));

        Assert.Equal(["Documents.StoragePath"], DatabaseInitializer.FindMissingColumns(db.Model, columns));
    }

    private static HashSet<(string, string)> ExpectedColumns(DocumentProcessingDbContext db) =>
        db.Model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null)
            .SelectMany(t => t.GetProperties().Select(p => (
                t.GetTableName()!,
                p.GetColumnName(Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(t.GetTableName()!, t.GetSchema()))!)))
            .Where(c => c.Item2 is not null)
            .ToHashSet();
}

/// <summary>
/// Runs <see cref="DatabaseInitializer"/> against a real PostgreSQL server. Each test uses its own
/// throwaway database. Skipped unless DOCPROC_TEST_POSTGRES holds a connection string (CI sets it).
/// </summary>
public class DatabaseInitializerPostgresTests
{
    [PostgresFact]
    public async Task InitializeAsync_NewDatabase_CreatesSchemaViaMigrations()
    {
        await using var scope = await TestDatabase.CreateAsync();

        await DatabaseInitializer.InitializeAsync(scope.Db, NullLogger.Instance);

        Assert.Equal(scope.Db.Database.GetMigrations(), await scope.Db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await scope.Db.Documents.CountAsync());
    }

    [PostgresFact]
    public async Task InitializeAsync_LegacyEnsureCreatedDatabase_BaselinesAndKeepsData()
    {
        await using var scope = await TestDatabase.CreateAsync();
        await scope.Db.Database.EnsureCreatedAsync();
        scope.Db.Documents.Add(new Document { FileName = "legacy.pdf", FileType = FileType.Pdf });
        await scope.Db.SaveChangesAsync();

        await DatabaseInitializer.InitializeAsync(scope.Db, NullLogger.Instance);

        Assert.Equal(scope.Db.Database.GetMigrations(), await scope.Db.Database.GetAppliedMigrationsAsync());
        Assert.Equal("legacy.pdf", (await scope.Db.Documents.SingleAsync()).FileName);
    }

    [PostgresFact]
    public async Task InitializeAsync_LegacyDatabaseWithDriftedSchema_ThrowsWithoutChanges()
    {
        await using var scope = await TestDatabase.CreateAsync();
        await scope.Db.Database.EnsureCreatedAsync();
        await scope.Db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Documents" DROP COLUMN "Source" """);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseInitializer.InitializeAsync(scope.Db, NullLogger.Instance));

        Assert.Contains("Documents.Source", ex.Message);
        Assert.Empty(await scope.Db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task InitializeAsync_RunTwice_IsIdempotent()
    {
        await using var scope = await TestDatabase.CreateAsync();

        await DatabaseInitializer.InitializeAsync(scope.Db, NullLogger.Instance);
        await DatabaseInitializer.InitializeAsync(scope.Db, NullLogger.Instance);

        Assert.Equal(scope.Db.Database.GetMigrations(), await scope.Db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task InitializeAsync_ConcurrentReplicasOnLegacyDatabase_BaselineOnce()
    {
        await using var scope = await TestDatabase.CreateAsync();
        await scope.Db.Database.EnsureCreatedAsync();

        await using var replicaA = scope.NewContext();
        await using var replicaB = scope.NewContext();

        await Task.WhenAll(
            DatabaseInitializer.InitializeAsync(replicaA, NullLogger.Instance),
            DatabaseInitializer.InitializeAsync(replicaB, NullLogger.Instance));

        Assert.Equal(scope.Db.Database.GetMigrations(), await scope.Db.Database.GetAppliedMigrationsAsync());
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private TestDatabase(string connectionString)
        {
            _connectionString = connectionString;
            Db = NewContext();
        }

        public DocumentProcessingDbContext Db { get; }

        public static Task<TestDatabase> CreateAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString)
            {
                Database = $"docproc_test_{Guid.NewGuid():N}",
                Pooling = false
            };
            return Task.FromResult(new TestDatabase(builder.ConnectionString));
        }

        public DocumentProcessingDbContext NewContext() =>
            new(new DbContextOptionsBuilder<DocumentProcessingDbContext>().UseNpgsql(_connectionString).Options);

        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("DOCPROC_TEST_POSTGRES");

    public PostgresFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            Skip = "Set DOCPROC_TEST_POSTGRES to a PostgreSQL connection string to run";
    }
}
