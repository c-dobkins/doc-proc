using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Core.Models;

/// <summary>
/// Represents a document uploaded for processing.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string FileName { get; set; }

    public FileType FileType { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    public long FileSizeBytes { get; set; }

    public string? StoragePath { get; set; }

    public string? Source { get; set; }

    public Guid? TemplateId { get; set; }

    public ExtractionTemplate? Template { get; set; }

    public ProcessingResult? ProcessingResult { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }
}
