namespace DocumentProcessing.Core.Models;

/// <summary>
/// Configuration for document processing pipeline behaviour.
/// </summary>
public class ProcessingOptions
{
    public const string SectionName = "Processing";

    /// <summary>
    /// Confidence score at or above which a document is auto-approved (status → Completed).
    /// Anything below goes to human review (status → NeedsReview).
    /// </summary>
    public double AutoApproveThreshold { get; set; } = 90.0;

    /// <summary>
    /// Maximum number of documents the background worker processes in parallel.
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;
}
