using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Applies business rules and validation to extraction results.
/// </summary>
public interface IPostProcessor
{
    Task<ProcessingResult> ProcessAsync(Document document, ExtractionResult extractionResult, string rawOcrText, CancellationToken ct);
}
