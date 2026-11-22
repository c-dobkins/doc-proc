namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Internal seam for Tesseract OCR. Implemented by TesseractOcrEngine.
/// Used by OcrEngineRouter to delegate scanned page recognition.
/// </summary>
public interface ITesseractEngine
{
    /// <summary>
    /// Runs OCR on an image stream already normalized by DocumentPreProcessor.
    /// </summary>
    Task<TesseractPageResult> RecognizeAsync(Stream imageStream, CancellationToken ct);

    /// <summary>
    /// Runs OCR on a PDF page rendered as an image byte array (no pre-processing).
    /// </summary>
    TesseractPageResult Recognize(byte[] imageBytes);
}
