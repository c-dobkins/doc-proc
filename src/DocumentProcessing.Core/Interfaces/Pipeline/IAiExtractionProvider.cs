using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Sends OCR text to an LLM and returns structured extraction results.
/// Per ADR-0003: all providers implement this common interface.
/// </summary>
public interface IAiExtractionProvider
{
    string Name { get; }

    Task<ExtractionResult> ExtractAsync(string ocrText, ExtractionTemplate template, CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);
}
