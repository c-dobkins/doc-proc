namespace DocumentProcessing.Core.Enums;

/// <summary>
/// Represents the lifecycle status of a document in the processing pipeline.
/// </summary>
public enum DocumentStatus
{
    Uploaded,
    Processing,
    Completed,
    Failed,
    NeedsReview,
    Approved
}
