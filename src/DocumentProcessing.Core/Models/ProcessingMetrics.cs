namespace DocumentProcessing.Core.Models;

/// <summary>
/// Tracks performance metrics for a single document processing run.
/// </summary>
public class ProcessingMetrics
{
    public long OcrDurationMs { get; set; }

    public long AiDurationMs { get; set; }

    public long TotalDurationMs { get; set; }

    public int TokenCount { get; set; }

    public decimal? EstimatedCost { get; set; }
}
