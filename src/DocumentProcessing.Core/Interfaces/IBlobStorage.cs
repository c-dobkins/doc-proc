namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Stores and retrieves original document files (local FS or Azure Blob).
/// </summary>
public interface IBlobStorage
{
    Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct);

    Task<Stream> GetAsync(string storagePath, CancellationToken ct);

    Task DeleteAsync(string storagePath, CancellationToken ct);
}
