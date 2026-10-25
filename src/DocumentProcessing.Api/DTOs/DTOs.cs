namespace DocumentProcessing.Api.DTOs;

public record DocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    string Status,
    long FileSizeBytes,
    string? Source,
    Guid? TemplateId,
    double? ConfidenceScore,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt);

public record DocumentDetailDto(
    Guid Id,
    string FileName,
    string FileType,
    string Status,
    long FileSizeBytes,
    string? Source,
    Guid? TemplateId,
    ProcessingResultDto? ProcessingResult,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt);

public record ProcessingResultDto(
    Guid Id,
    List<DocumentFieldDto> ExtractedFields,
    double ConfidenceScore,
    string? RawOcrText,
    string? AiProvider,
    ProcessingMetricsDto Metrics,
    DateTimeOffset CreatedAt);

public record DocumentFieldDto(
    Guid Id,
    string Name,
    string? Value,
    double Confidence,
    string DataType);

public record ProcessingMetricsDto(
    long OcrDurationMs,
    long AiDurationMs,
    long TotalDurationMs,
    int TokenCount,
    decimal? EstimatedCost);

public record UploadDocumentResponse(
    Guid DocumentId,
    string FileName,
    string Status);

public record UpdateFieldsRequest(
    List<FieldUpdateDto> Fields);

public record FieldUpdateDto(
    string Name,
    string? Value);

public record CreateTemplateRequest(
    string Name,
    string? Description,
    List<TemplateFieldDto> Fields,
    string PromptTemplate);

public record UpdateTemplateRequest(
    string Name,
    string? Description,
    List<TemplateFieldDto> Fields,
    string PromptTemplate);

public record TemplateFieldDto(
    string Name,
    string DataType,
    bool IsRequired,
    string? Description);

public record TemplateDto(
    Guid Id,
    string Name,
    string? Description,
    List<TemplateFieldDto> Fields,
    string PromptTemplate,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record DashboardMetricsDto(
    int TotalDocuments,
    int Uploaded,
    int Processing,
    int Completed,
    int Failed,
    int NeedsReview,
    int Approved);

public record AuthStatusDto(
    bool WritesRequireKey,
    bool CanWrite);

/// <summary>
/// Payload of the SignalR "StatusUpdated" message sent to a document's group.
/// </summary>
public record StatusUpdatedMessage(
    Guid DocumentId,
    string Status);
