using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Validates and prepares documents for OCR processing.
/// PDFs pass through unchanged (the OCR router decides digital vs scanned per page).
/// Images are decoded with the coder for their declared type and normalized for Tesseract:
/// EXIF auto-orientation, grayscale and a contrast boost, re-encoded as PNG.
/// </summary>
public class DocumentPreProcessor(ILogger<DocumentPreProcessor> logger) : IPreProcessor
{
    private static readonly HashSet<FileType> SupportedFileTypes =
    [
        FileType.Pdf,
        FileType.Jpeg,
        FileType.Png,
        FileType.Tiff,
        FileType.Bmp
    ];

    public async Task<PreProcessingResult> ProcessAsync(Document document, Stream fileStream, CancellationToken ct)
    {
        logger.LogInformation("Pre-processing document {DocumentId} ({FileName}, {FileType})",
            document.Id, document.FileName, document.FileType);

        if (!SupportedFileTypes.Contains(document.FileType))
        {
            return new PreProcessingResult
            {
                IsValid = false,
                ValidationError = $"Unsupported file type: {document.FileType}"
            };
        }

        if (fileStream.Length == 0)
        {
            return new PreProcessingResult
            {
                IsValid = false,
                ValidationError = "File is empty"
            };
        }

        // Ensure we're at the start of the stream
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        return document.FileType == FileType.Pdf
            ? PreProcessPdf(document, fileStream)
            : await PreProcessImageAsync(document, fileStream, ct);
    }

    private PreProcessingResult PreProcessPdf(Document document, Stream fileStream)
    {
        // For PDFs, we pass the stream directly — the OCR engine will decide
        // whether to use PdfPig (digital) or Tesseract (scanned).
        var copiedStream = new MemoryStream();
        fileStream.CopyTo(copiedStream);
        copiedStream.Position = 0;

        logger.LogDebug("PDF pre-processing complete for {DocumentId}, size={Size} bytes",
            document.Id, copiedStream.Length);

        return new PreProcessingResult
        {
            IsValid = true,
            PreparedPages = [copiedStream],
            IsImageBased = false, // OCR engine will determine this per-page
            PageCount = 1 // Will be updated by OCR engine
        };
    }

    private async Task<PreProcessingResult> PreProcessImageAsync(Document document, Stream fileStream, CancellationToken ct)
    {
        try
        {
            // Decoding doubles as validation: corrupt, oversized or mislabelled images throw here
            using var image = new MagickImage();
            await image.ReadAsync(fileStream, ImageDecoding.ReadSettingsFor(document.FileType), ct);

            logger.LogDebug("Image pre-processing for {DocumentId}: {Width}x{Height}",
                document.Id, image.Width, image.Height);

            image.AutoOrient();
            image.Grayscale();
            image.BrightnessContrast(new Percentage(0), new Percentage(30));

            var preparedStream = new MemoryStream();
            await image.WriteAsync(preparedStream, MagickFormat.Png, ct);
            preparedStream.Position = 0;

            return new PreProcessingResult
            {
                IsValid = true,
                PreparedPages = [preparedStream],
                IsImageBased = true,
                PageCount = 1
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate image for document {DocumentId}", document.Id);
            return new PreProcessingResult
            {
                IsValid = false,
                ValidationError = $"Invalid or corrupted image file: {ex.Message}"
            };
        }
    }
}
