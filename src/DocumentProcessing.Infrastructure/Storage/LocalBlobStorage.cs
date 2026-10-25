using DocumentProcessing.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Storage;

public class LocalBlobStorageOptions
{
    public string BasePath { get; set; } = "uploads";
}

/// <summary>
/// Local file system implementation of blob storage (development, tests, or a mounted volume).
/// </summary>
public class LocalBlobStorage(IOptions<LocalBlobStorageOptions> options) : IBlobStorage
{
    private readonly string _basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.BasePath));

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        var relativePath = StoragePaths.NewRelativePath(fileName);

        var fullPath = ResolveFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        await using var fileOutputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fileOutputStream, ct);

        return relativePath;
    }

    /// <summary>
    /// Returns a readable stream for the stored file.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    public Task<Stream> GetAsync(string storagePath, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storagePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {storagePath}");

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storagePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a stored relative path to an absolute path, rejecting anything that escapes
    /// the base directory (including sibling directories that merely share its prefix).
    /// </summary>
    private string ResolveFullPath(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, StoragePaths.Normalize(storagePath)));
        var relative = Path.GetRelativePath(_basePath, fullPath);

        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException(
                $"Storage path '{storagePath}' resolves outside the storage base directory.");

        return fullPath;
    }
}
