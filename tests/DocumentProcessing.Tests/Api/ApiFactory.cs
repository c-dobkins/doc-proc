using DocumentProcessing.Infrastructure.Data;
using DocumentProcessing.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DocumentProcessing.Tests.Api;

/// <summary>
/// Hosts the real API pipeline in-memory, swapping PostgreSQL for a shared SQLite in-memory
/// database, pointing blob storage at a temp directory, and disabling the background worker
/// so uploaded documents stay in their initial state.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected virtual int ProcessingPermitsPerMinute => 1000;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _blobDir = Path.Combine(Path.GetTempPath(), "docproc-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("BlobStorage:BasePath", _blobDir);
        builder.UseSetting("RateLimiting:ProcessingPermitsPerMinute", ProcessingPermitsPerMinute.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DocumentProcessingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DocumentProcessingDbContext>>();
            services.AddDbContext<DocumentProcessingDbContext>(options => options
                .UseSqlite(_connection)
                .ReplaceService<IValueGeneratorSelector, SqliteOwnedKeyValueGeneratorSelector>()
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());

            var worker = services.Single(d => d.ImplementationType == typeof(DocumentProcessingWorker));
            services.Remove(worker);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(_blobDir))
                Directory.Delete(_blobDir, recursive: true);
        }
    }
}

/// <summary>
/// PostgreSQL generates the int part of TemplateField's composite (owner, Id) key as an identity
/// column; SQLite can't autoincrement composite keys, so generate it client-side in tests.
/// </summary>
internal sealed class SqliteOwnedKeyValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies)
    : RelationalValueGeneratorSelector(dependencies)
{
    public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (typeBase.ClrType == typeof(TemplateField) && property.IsKey() && property.ClrType == typeof(int))
        {
            valueGenerator = new IncrementingIntGenerator();
            return true;
        }

        return base.TrySelect(property, typeBase, out valueGenerator);
    }

    private sealed class IncrementingIntGenerator : ValueGenerator<int>
    {
        private static int _next;

        public override bool GeneratesTemporaryValues => false;

        public override int Next(EntityEntry entry) => Interlocked.Increment(ref _next);
    }
}

/// <summary>
/// SQLite can't ORDER BY DateTimeOffset; store those columns as sortable binary in tests only.
/// </summary>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}

/// <summary>
/// WebApplicationFactory hosts share the API's static entry point (and Serilog's static logger),
/// so building two concurrently can intercept each other. Run all API host tests serially.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiHostCollection
{
    public const string Name = "API host";
}
