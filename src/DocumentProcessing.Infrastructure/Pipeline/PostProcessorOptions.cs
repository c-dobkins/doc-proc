namespace DocumentProcessing.Infrastructure.Pipeline;

/// <summary>
/// Configuration for confidence penalties applied during post-processing.
/// Penalties drive document routing (Completed vs NeedsReview) so they
/// should be tuned to match business tolerance for extraction errors.
/// </summary>
public class PostProcessorOptions
{
    public const string SectionName = "PostProcessing";

    /// <summary>
    /// Confidence penalty (percentage points) applied when a required field
    /// is present but has an empty or whitespace-only value.
    /// Default: 20.0
    /// </summary>
    public double MissingRequiredFieldPenalty { get; set; } = 20.0;

    /// <summary>
    /// Confidence penalty (percentage points) applied when a field value
    /// fails type-specific validation (e.g., invalid date, non-numeric currency).
    /// Default: 15.0
    /// </summary>
    public double ValidationFailurePenalty { get; set; } = 15.0;
}
