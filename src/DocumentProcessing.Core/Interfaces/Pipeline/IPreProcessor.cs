using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Validates, converts, and prepares documents for OCR processing.
/// </summary>
public interface IPreProcessor
{
    Task<PreProcessingResult> ProcessAsync(Document document, Stream fileStream, CancellationToken ct);
}

/// <summary>
/// Result of pre-processing a document before OCR.
/// </summary>
public class PreProcessingResult
{
    public bool IsValid { get; set; }

    public string? ValidationError { get; set; }

    /// <summary>
    /// Prepared images/streams for OCR (one per page for multi-page docs).
    /// </summary>
    public List<Stream> PreparedPages { get; set; } = [];

    public bool IsImageBased { get; set; }

    public int PageCount { get; set; }
}
