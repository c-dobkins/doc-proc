using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Core.Models;

/// <summary>
/// A file entering the system, from any source (HTTP upload today; e.g. email or a watched folder later).
/// </summary>
public record IngestionRequest(
    Stream Content,
    string FileName,
    FileType FileType,
    long SizeBytes,
    string Source,
    Guid? TemplateId = null);
