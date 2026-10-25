using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Extracts text from digital PDFs using PdfPig.
/// Fast (&lt; 100ms/page) for PDFs with embedded text layers.
/// Returns empty/minimal text for scanned PDFs (indicating Tesseract should be used).
/// </summary>
public class PdfPigTextExtractor(ILogger<PdfPigTextExtractor> logger) : IPdfTextExtractor
{
    /// <summary>
    /// Minimum characters per page to consider the page as having a text layer.
    /// Below this threshold, the page is likely scanned/image-based.
    /// </summary>
    private const int MinCharsPerPage = 10;

    /// <summary>
    /// Attempts to extract text from a PDF stream using PdfPig.
    /// </summary>
    /// <returns>Extraction result with per-page text and whether the PDF has text layers.</returns>
    public PdfExtractionResult Extract(Stream pdfStream)
    {
        var stopwatch = Stopwatch.StartNew();
        var pageTexts = new List<PageText>();
        int scannedPageCount = 0;

        try
        {
            using var document = PdfDocument.Open(pdfStream);
            int totalPages = document.NumberOfPages;

            for (int i = 1; i <= totalPages; i++)
            {
                var page = document.GetPage(i);
                var text = page.Text ?? string.Empty;
                var trimmedText = text.Trim();

                bool hasTextLayer = trimmedText.Length >= MinCharsPerPage;

                if (!hasTextLayer)
                    scannedPageCount++;

                pageTexts.Add(new PageText
                {
                    PageNumber = i,
                    Text = trimmedText,
                    HasTextLayer = hasTextLayer
                });
            }

            stopwatch.Stop();
            bool allPagesScanned = scannedPageCount == totalPages;

            logger.LogDebug(
                "PdfPig extracted text from {PageCount} pages in {ElapsedMs}ms. Scanned pages: {ScannedCount}/{TotalCount}",
                totalPages, stopwatch.ElapsedMilliseconds, scannedPageCount, totalPages);

            return new PdfExtractionResult
            {
                Pages = pageTexts,
                TotalPages = totalPages,
                ScannedPageCount = scannedPageCount,
                IsFullyScanned = allPagesScanned,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "PdfPig failed to read PDF");

            return new PdfExtractionResult
            {
                Pages = [],
                TotalPages = 0,
                ScannedPageCount = 0,
                IsFullyScanned = true, // Assume scanned on failure — Tesseract will try
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Explicit interface implementation — callable via IPdfTextExtractor.
    /// </summary>
    string IPdfTextExtractor.CombinePageTexts(IReadOnlyList<PageText> pages) => CombinePageTextsStatic(pages);

    /// <summary>
    /// Static helper — available for callers that don't go through the interface.
    /// </summary>
    public static string CombinePageTexts(IReadOnlyList<PageText> pages) => CombinePageTextsStatic(pages);

    private static string CombinePageTextsStatic(IReadOnlyList<PageText> pages)
    {
        if (pages.Count == 0) return string.Empty;
        if (pages.Count == 1) return pages[0].Text;

        var sb = new StringBuilder();
        foreach (var page in pages)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            sb.AppendLine($"--- Page {page.PageNumber} ---");
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Result of PdfPig text extraction with per-page detail.
/// </summary>
public class PdfExtractionResult
{
    public List<PageText> Pages { get; set; } = [];
    public int TotalPages { get; set; }
    public int ScannedPageCount { get; set; }
    public bool IsFullyScanned { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Text extracted from a single PDF page.
/// </summary>
public class PageText
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool HasTextLayer { get; set; }
}
