namespace DocumentProcessing.Core.Models;

/// <summary>
/// Stores the result of AI extraction for a document.
/// </summary>
public class ProcessingResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public Document? Document { get; set; }

    public List<DocumentField> ExtractedFields { get; set; } = [];

    public double ConfidenceScore { get; set; }

    public string? RawOcrText { get; set; }

    public string? AiProvider { get; set; }

    /// <summary>
    /// Raw JSON response from the AI provider, preserved for debugging and audit.
    /// Populated from <see cref="ExtractionResult.RawResponse"/> by the post-processor.
    /// </summary>
    public string? RawAiResponse { get; set; }

    public ProcessingMetrics Metrics { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
