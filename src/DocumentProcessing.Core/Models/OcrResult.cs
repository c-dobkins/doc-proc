namespace DocumentProcessing.Core.Models;

/// <summary>
/// Result of OCR text extraction from a document.
/// </summary>
public class OcrResult
{
    public required string Text { get; set; }

    public bool IsScanned { get; set; }

    public int PageCount { get; set; }

    public long ProcessingTimeMs { get; set; }

    public string? EngineUsed { get; set; }
}
