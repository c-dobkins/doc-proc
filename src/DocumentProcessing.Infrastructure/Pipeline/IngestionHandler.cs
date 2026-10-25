using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Pipeline;

/// <summary>
/// Saves the file to blob storage, then persists the <see cref="Document"/> record.
/// If the record can't be saved, the stored file is deleted so no orphaned blobs are left behind.
/// </summary>
public class IngestionHandler(
    IBlobStorage blobStorage,
    IDocumentRepository documentRepository,
    ILogger<IngestionHandler> logger) : IIngestionHandler
{
    public async Task<Document> IngestAsync(IngestionRequest request, CancellationToken ct)
    {
        var storagePath = await blobStorage.SaveAsync(request.Content, request.FileName, ct);

        var document = new Document
        {
            FileName = request.FileName,
            FileType = request.FileType,
            FileSizeBytes = request.SizeBytes,
            StoragePath = storagePath,
            Source = request.Source,
            TemplateId = request.TemplateId
        };

        try
        {
            await documentRepository.AddAsync(document, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist ingested document {FileName}; removing stored file", request.FileName);
            await blobStorage.DeleteAsync(storagePath, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Ingested document {DocumentId} ({FileName}, {FileType}, {SizeBytes} bytes) from {Source}",
            document.Id, document.FileName, document.FileType, document.FileSizeBytes, document.Source);

        return document;
    }
}
