using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Extracts text from documents using OCR or direct text extraction.
/// Implements dual strategy per ADR-0005: PdfPig for digital PDFs, Tesseract for scanned docs.
/// </summary>
public interface IOcrEngine
{
    Task<OcrResult> ExtractTextAsync(Document document, Stream fileStream, CancellationToken ct);
}
