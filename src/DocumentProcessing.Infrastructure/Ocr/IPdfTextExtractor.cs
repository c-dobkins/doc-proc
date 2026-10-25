namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Internal seam for PDF text extraction. Implemented by PdfPigTextExtractor.
/// Used by OcrEngineRouter to route between digital and scanned extraction strategies.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Attempts to extract text from a PDF stream.
    /// Returns per-page results indicating which pages have text layers.
    /// </summary>
    PdfExtractionResult Extract(Stream pdfStream);

    /// <summary>
    /// Combines per-page texts into a single string with page markers.
    /// </summary>
    string CombinePageTexts(IReadOnlyList<PageText> pages);
}
