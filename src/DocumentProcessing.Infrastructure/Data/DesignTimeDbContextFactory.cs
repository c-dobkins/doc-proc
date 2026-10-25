using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocumentProcessing.Infrastructure.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling. Creating a migration does not connect to a database;
/// set <c>ConnectionStrings__documentprocessing</c> to point commands like <c>database update</c> at a real one.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DocumentProcessingDbContext>
{
    public DocumentProcessingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__documentprocessing")
            ?? "Host=localhost;Port=5432;Database=documentprocessing;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<DocumentProcessingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DocumentProcessingDbContext(options);
    }
}
