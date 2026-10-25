using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DocumentProcessing.Infrastructure.Data;

/// <summary>
/// Brings the database schema up to date at startup.
///
/// PostgreSQL: applies EF Core migrations. Databases created before migrations existed (by
/// <c>EnsureCreated</c>) are first "baselined": if their schema already contains every table and
/// column of the initial migration, that migration is recorded as applied instead of re-run.
/// If the schema doesn't match, startup fails with instructions rather than guessing.
///
/// Other providers (SQLite in tests) have no migrations and use <c>EnsureCreated</c>.
/// </summary>
public static class DatabaseInitializer
{
    // Serializes schema bootstrapping when several replicas start at once. The key is arbitrary but fixed.
    private const string AcquireLockSql = "SELECT pg_advisory_xact_lock(7570418296201059)";

    public static async Task InitializeAsync(DocumentProcessingDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (!db.Database.IsNpgsql())
        {
            await db.Database.EnsureCreatedAsync(ct);
            return;
        }

        // Check existence via the database creator: unlike CanConnect/GetPendingMigrations it doesn't
        // log a connection error for a database that simply hasn't been created yet.
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(ct))
        {
            logger.LogInformation("Database does not exist yet; creating it from migrations");
            try
            {
                await creator.CreateAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
            {
                // Another replica created it first
            }
        }

        var history = db.GetService<IHistoryRepository>();
        await EnsureHistoryTableAsync(db, history, ct);
        await BaselineLegacyDatabaseAsync(db, history, logger, ct);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
            logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, pending);

        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Returns the (table, column) pairs the model maps to that are absent from <paramref name="existingColumns"/>.
    /// </summary>
    public static IReadOnlyList<string> FindMissingColumns(IModel model, IReadOnlySet<(string Table, string Column)> existingColumns)
    {
        var missing = new List<string>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (table is null)
                continue;

            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(storeObject);
                if (column is not null && !existingColumns.Contains((table, column)))
                    missing.Add($"{table}.{column}");
            }
        }

        return missing.Distinct().Order().ToList();
    }

    /// <summary>
    /// Creates the migrations history table if it's missing. EF's Npgsql history repository can report
    /// a missing table as present and then fail querying it (logging an error, or aborting an open
    /// transaction), so it is created up front. Done under the advisory lock because concurrent
    /// <c>CREATE TABLE IF NOT EXISTS</c> statements can still collide in PostgreSQL.
    /// </summary>
    private static async Task EnsureHistoryTableAsync(DocumentProcessingDbContext db, IHistoryRepository history, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(AcquireLockSql, ct);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task BaselineLegacyDatabaseAsync(
        DocumentProcessingDbContext db, IHistoryRepository history, ILogger logger, CancellationToken ct)
    {
        // Any recorded migration means the database is already managed by migrations
        if ((await db.Database.GetAppliedMigrationsAsync(ct)).Any())
            return;

        var existingColumns = await ReadExistingColumnsAsync(db, HistoryRepository.DefaultTableName, ct);
        if (existingColumns.Count == 0)
            return; // empty database: let migrations create everything

        var missing = FindMissingColumns(db.Model, existingColumns);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The database was created without migrations and its schema does not match the current model " +
                $"(missing: {string.Join(", ", missing)}). It cannot be baselined safely. Back up any data you need, " +
                "then drop and recreate the database (it will be recreated and re-seeded on the next start).");
        }

        var initialMigration = db.Database.GetMigrations().First();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(AcquireLockSql, ct);

        // Re-check under the lock: another replica may have baselined while we waited
        if (!(await db.Database.GetAppliedMigrationsAsync(ct)).Any())
        {
            await db.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), ct);

            logger.LogWarning(
                "Baselined existing database: recorded migration {Migration} as applied (schema was created by EnsureCreated)",
                initialMigration);
        }

        await transaction.CommitAsync(ct);
    }

    private static async Task<HashSet<(string Table, string Column)>> ReadExistingColumnsAsync(
        DocumentProcessingDbContext db, string historyTable, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQueryRaw<ColumnRow>(
                """
                SELECT table_name AS "Table", column_name AS "Column"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                """)
            .ToListAsync(ct);

        return rows
            .Where(r => r.Table != historyTable)
            .Select(r => (r.Table, r.Column))
            .ToHashSet();
    }

    private sealed class ColumnRow
    {
        public required string Table { get; init; }
        public required string Column { get; init; }
    }
}
