using System.Diagnostics;
using System.Text;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Implements the dual OCR strategy per ADR-0005:
/// 1. For PDFs: Try PdfPig first for digital text extraction
/// 2. If pages lack text layers (scanned), fall back to Tesseract OCR
/// 3. For images: Go directly to Tesseract OCR
/// </summary>
public class OcrEngineRouter(
    IPdfTextExtractor pdfPigExtractor,
    ITesseractEngine tesseractEngine,
    ILogger<OcrEngineRouter> logger) : IOcrEngine
{
    public async Task<OcrResult> ExtractTextAsync(Document document, Stream fileStream, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();

        logger.LogInformation("Starting OCR for document {DocumentId} ({FileName}, type={FileType})",
            document.Id, document.FileName, document.FileType);

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        var result = document.FileType == FileType.Pdf
            ? ExtractFromPdf(document, fileStream, ct)
            : await ExtractFromImageAsync(document, fileStream, ct);

        totalStopwatch.Stop();
        result.ProcessingTimeMs = totalStopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "OCR complete for {DocumentId}: engine={Engine}, scanned={IsScanned}, pages={Pages}, chars={Chars}, time={Time}ms",
            document.Id, result.EngineUsed, result.IsScanned, result.PageCount,
            result.Text.Length, result.ProcessingTimeMs);

        return result;
    }

    private OcrResult ExtractFromPdf(Document document, Stream pdfStream, CancellationToken ct)
    {
        // Step 1: Try PdfPig for digital text extraction
        var pdfResult = pdfPigExtractor.Extract(pdfStream);

        if (pdfResult.Error is not null)
        {
            logger.LogWarning("PdfPig failed for {DocumentId}: {Error}. Falling back to Tesseract.",
                document.Id, pdfResult.Error);

            // PdfPig couldn't read the PDF at all — treat entire doc as scanned
            if (pdfStream.CanSeek)
                pdfStream.Position = 0;

            return OcrScannedPdf(document, pdfStream, ct);
        }

        // Step 2: Check if all pages have text layers
        if (!pdfResult.IsFullyScanned)
        {
            // All or some pages have text — use PdfPig text for digital pages
            if (pdfResult.ScannedPageCount == 0)
            {
                // Pure digital PDF — return PdfPig text directly
                return new OcrResult
                {
                    Text = pdfPigExtractor.CombinePageTexts(pdfResult.Pages),
                    IsScanned = false,
                    PageCount = pdfResult.TotalPages,
                    EngineUsed = "PdfPig"
                };
            }

            // Mixed PDF: some pages digital, some scanned
            // Use PdfPig text for digital pages, Tesseract for scanned pages
            logger.LogInformation(
                "Mixed PDF detected for {DocumentId}: {Digital} digital, {Scanned} scanned pages",
                document.Id, pdfResult.TotalPages - pdfResult.ScannedPageCount, pdfResult.ScannedPageCount);

            return ExtractMixedPdf(document, pdfResult, pdfStream, ct);
        }

        // Step 3: Fully scanned PDF — fall back entirely to Tesseract
        logger.LogInformation("Fully scanned PDF detected for {DocumentId}, using Tesseract", document.Id);

        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        return OcrScannedPdf(document, pdfStream, ct);
    }

    private OcrResult ExtractMixedPdf(Document document, PdfExtractionResult pdfResult, Stream pdfStream, CancellationToken ct)
    {
        // Digital pages use the PdfPig text layer; pages without one are OCR'd from their
        // embedded scan image. If the PDF can't be re-opened for image extraction, scanned
        // pages fall back to a placeholder so the digital text is still returned.
        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        UglyToad.PdfPig.PdfDocument? pdfDoc = null;
        try
        {
            pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open mixed PDF {DocumentId} for image extraction; scanned pages will be skipped", document.Id);
        }

        using (pdfDoc)
        {
            var sb = new StringBuilder();

            foreach (var page in pdfResult.Pages)
            {
                ct.ThrowIfCancellationRequested();

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.AppendLine($"--- Page {page.PageNumber} ---");

                var text = page.HasTextLayer
                    ? page.Text
                    : pdfDoc is not null ? OcrLargestPageImage(document, pdfDoc.GetPage(page.PageNumber)) : null;

                sb.AppendLine(string.IsNullOrWhiteSpace(text) ? "[Scanned page — text extraction limited]" : text);
            }

            return new OcrResult
            {
                Text = sb.ToString(),
                IsScanned = true,
                PageCount = pdfResult.TotalPages,
                EngineUsed = "PdfPig+Tesseract"
            };
        }
    }

    private OcrResult OcrScannedPdf(Document document, Stream pdfStream, CancellationToken ct)
    {
        // Scanned PDFs contain embedded images. Extract them with PdfPig
        // and run Tesseract on each page image.
        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        var sb = new StringBuilder();
        int pageCount = 0;

        try
        {
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
            pageCount = pdfDoc.NumberOfPages;

            for (int i = 1; i <= pageCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var text = OcrLargestPageImage(document, pdfDoc.GetPage(i));
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0) sb.AppendLine();
                if (pageCount > 1) sb.AppendLine($"--- Page {i} ---");
                sb.AppendLine(text);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract images from scanned PDF {DocumentId}", document.Id);
        }

        return new OcrResult
        {
            Text = sb.ToString(),
            IsScanned = true,
            PageCount = pageCount > 0 ? pageCount : 1,
            EngineUsed = "Tesseract"
        };
    }

    /// <summary>
    /// Runs Tesseract on the largest embedded image of a page (typically the full-page scan).
    /// Returns null when the page has no usable image.
    /// </summary>
    private string? OcrLargestPageImage(Document document, UglyToad.PdfPig.Content.Page page)
    {
        var images = page.GetImages().ToList();

        if (images.Count == 0)
        {
            logger.LogDebug("No images found on page {Page} of PDF {DocumentId}", page.Number, document.Id);
            return null;
        }

        var largestImage = images.OrderByDescending(img => img.BoundingBox.Width * img.BoundingBox.Height).First();

        byte[] imageBytes;
        if (largestImage.TryGetPng(out var pngBytes))
            imageBytes = pngBytes;
        else if (largestImage.RawBytes.Length > 0)
            imageBytes = largestImage.RawBytes.ToArray();
        else
        {
            logger.LogWarning("Could not extract image bytes from page {Page} of {DocumentId}", page.Number, document.Id);
            return null;
        }

        return tesseractEngine.Recognize(imageBytes).Text;
    }

    private async Task<OcrResult> ExtractFromImageAsync(Document document, Stream imageStream, CancellationToken ct)
    {
        logger.LogDebug("Processing image document {DocumentId} with Tesseract", document.Id);

        var result = await tesseractEngine.RecognizeAsync(imageStream, ct);

        if (result.Error is not null)
        {
            logger.LogError("Tesseract OCR failed for image {DocumentId}: {Error}", document.Id, result.Error);
        }

        return new OcrResult
        {
            Text = result.Text,
            IsScanned = true,
            PageCount = 1,
            EngineUsed = "Tesseract"
        };
    }
}
