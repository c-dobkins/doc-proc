using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentProcessing.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation, for deployments where the container filesystem is ephemeral.
/// Paths match <see cref="LocalBlobStorage"/> (<c>yyyy/MM/dd/{guid}_{name}</c>), so the same
/// <c>StoragePath</c> values work with either backend.
/// </summary>
public class AzureBlobStorage(BlobContainerClient container, ILogger<AzureBlobStorage> logger) : IBlobStorage
{
    private readonly SemaphoreSlim _containerLock = new(1, 1);
    private volatile bool _containerReady;

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);

        var relativePath = StoragePaths.NewRelativePath(fileName);
        var contentType = ContentTypeFor(fileName);

        await container.GetBlobClient(relativePath).UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            // Paths are unique per upload; never silently overwrite an existing blob
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, ct);

        return relativePath;
    }

    /// <summary>
    /// Returns a readable stream for the stored blob. The caller disposes it.
    /// </summary>
    public async Task<Stream> GetAsync(string storagePath, CancellationToken ct)
    {
        var blob = container.GetBlobClient(Validate(storagePath));

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return download.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"File not found: {storagePath}", ex);
        }
    }

    public async Task DeleteAsync(string storagePath, CancellationToken ct)
    {
        await container.GetBlobClient(Validate(storagePath)).DeleteIfExistsAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Blob names are flat, but reject traversal-looking paths so both backends share one contract.
    /// </summary>
    private static string Validate(string storagePath)
    {
        var normalized = StoragePaths.Normalize(storagePath);

        if (normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is ".." or "."))
            throw new UnauthorizedAccessException($"Storage path '{storagePath}' is not a valid relative path.");

        return normalized;
    }

    private static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".tif" or ".tiff" => "image/tiff",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream"
    };

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerReady)
            return;

        await _containerLock.WaitAsync(ct);
        try
        {
            if (!_containerReady)
            {
                var created = await container.CreateIfNotExistsAsync(cancellationToken: ct);
                if (created is not null)
                    logger.LogInformation("Created blob container {Container}", container.Name);
                _containerReady = true;
            }
        }
        finally
        {
            _containerLock.Release();
        }
    }
}
