using DocumentProcessing.Api.Auth;
using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Api.Middleware;
using DocumentProcessing.Api.Validators;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessing.Api.Endpoints;

public static class DocumentEndpoints
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;
    public const string ProcessingRateLimitPolicy = "processing";

    public static RouteGroupBuilder MapDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/documents").WithTags("Documents");

        group.MapPost("/upload", UploadDocument)
            .RequireAuthorization(AuthPolicies.Write)
            .DisableAntiforgery()
            // Kestrel's default body limit (~28.6 MB) would otherwise reject uploads below our 50 MB cap
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes + 1024 * 1024))
            .RequireRateLimiting(ProcessingRateLimitPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .Produces<UploadDocumentResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetDocuments)
            .Produces<PagedResponse<DocumentDto>>();

        group.MapGet("/{id:guid}", GetDocumentById)
            .Produces<DocumentDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/fields", UpdateFields)
            .RequireAuthorization(AuthPolicies.Write)
            .AddEndpointFilter<ValidationFilter<UpdateFieldsRequest>>()
            .Produces<DocumentDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/approve", ApproveDocument)
            .RequireAuthorization(AuthPolicies.Write)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reprocess", ReprocessDocument)
            .RequireAuthorization(AuthPolicies.Write)
            .RequireRateLimiting(ProcessingRateLimitPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/download", DownloadDocument)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> UploadDocument(
        IFormFile file,
        Guid? templateId,
        IIngestionHandler ingestionHandler,
        DocumentProcessingChannel channel,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return Results.BadRequest("File is empty.");

        if (file.Length > MaxUploadBytes)
            return Results.BadRequest("File exceeds 50MB limit.");

        var fileType = ParseFileType(file.FileName);
        if (fileType is null)
            return Results.BadRequest($"Unsupported file type: {Path.GetExtension(file.FileName)}");

        await using var stream = file.OpenReadStream();

        if (!await FileSignatureValidator.MatchesAsync(stream, fileType.Value, ct))
            return Results.BadRequest($"File content does not match its {Path.GetExtension(file.FileName)} extension.");

        stream.Position = 0;
        var document = await ingestionHandler.IngestAsync(
            new IngestionRequest(stream, file.FileName, fileType.Value, file.Length, Source: "upload", templateId), ct);

        await channel.Writer.WriteAsync(document.Id, ct);

        return Results.Accepted(
            $"/api/documents/{document.Id}",
            new UploadDocumentResponse(document.Id, document.FileName, document.Status.ToString()));
    }

    private static async Task<IResult> GetDocuments(
        int page,
        int pageSize,
        DocumentStatus? status,
        IDocumentRepository documentRepository,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var documents = await documentRepository.GetAllAsync(page, pageSize, status, ct);
        var totalCount = await documentRepository.GetCountAsync(status, ct);

        var items = documents.Select(d => new DocumentDto(
            d.Id, d.FileName, d.FileType.ToString(), d.Status.ToString(),
            d.FileSizeBytes, d.Source, d.TemplateId,
            d.ProcessingResult?.ConfidenceScore, d.UploadedAt, d.ProcessedAt)).ToList();

        return Results.Ok(new PagedResponse<DocumentDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetDocumentById(
        Guid id,
        IDocumentRepository documentRepository,
        CancellationToken ct)
    {
        var document = await documentRepository.GetByIdAsync(id, ct);
        if (document is null)
            return Results.NotFound();

        return Results.Ok(MapToDetailDto(document));
    }

    private static async Task<IResult> UpdateFields(
        Guid id,
        UpdateFieldsRequest request,
        IDocumentRepository documentRepository,
        ICorrectionLogRepository correctionLogRepository,
        CancellationToken ct)
    {
        var document = await documentRepository.GetByIdAsync(id, ct);
        if (document?.ProcessingResult is null)
            return Results.NotFound();

        foreach (var fieldUpdate in request.Fields)
        {
            var existingField = document.ProcessingResult.ExtractedFields
                .FirstOrDefault(f => f.Name == fieldUpdate.Name);

            if (existingField is not null && existingField.Value != fieldUpdate.Value)
            {
                correctionLogRepository.Add(new CorrectionLog
                {
                    DocumentId = document.Id,
                    FieldName = fieldUpdate.Name,
                    OriginalValue = existingField.Value,
                    CorrectedValue = fieldUpdate.Value
                });

                existingField.Value = fieldUpdate.Value;
            }
        }

        await documentRepository.UpdateAsync(document, ct);
        return Results.Ok(MapToDetailDto(document));
    }

    private static async Task<IResult> ApproveDocument(
        Guid id,
        IDocumentRepository documentRepository,
        CancellationToken ct)
    {
        var document = await documentRepository.GetByIdAsync(id, ct);
        if (document is null)
            return Results.NotFound();

        document.Status = DocumentStatus.Approved;
        await documentRepository.UpdateAsync(document, ct);
        return Results.Ok();
    }

    private static async Task<IResult> ReprocessDocument(
        Guid id,
        IDocumentRepository documentRepository,
        DocumentProcessingChannel channel,
        CancellationToken ct)
    {
        var document = await documentRepository.GetByIdAsync(id, ct);
        if (document is null)
            return Results.NotFound();

        document.Status = DocumentStatus.Uploaded;
        await documentRepository.UpdateAsync(document, ct);
        await channel.Writer.WriteAsync(document.Id, ct);

        return Results.Accepted($"/api/documents/{document.Id}");
    }

    private static async Task<IResult> DownloadDocument(
        Guid id,
        IDocumentRepository documentRepository,
        IBlobStorage blobStorage,
        CancellationToken ct)
    {
        var document = await documentRepository.GetByIdAsync(id, ct);
        if (document?.StoragePath is null)
            return Results.NotFound();

        var stream = await blobStorage.GetAsync(document.StoragePath, ct);
        var contentType = document.FileType switch
        {
            FileType.Pdf => "application/pdf",
            FileType.Jpeg => "image/jpeg",
            FileType.Png => "image/png",
            FileType.Tiff => "image/tiff",
            FileType.Bmp => "image/bmp",
            _ => "application/octet-stream"
        };

        return Results.File(stream, contentType, document.FileName);
    }

    private static DocumentDetailDto MapToDetailDto(Document document) =>
        new(
            document.Id, document.FileName, document.FileType.ToString(),
            document.Status.ToString(), document.FileSizeBytes,
            document.Source, document.TemplateId,
            document.ProcessingResult is not null
                ? new ProcessingResultDto(
                    document.ProcessingResult.Id,
                    document.ProcessingResult.ExtractedFields.Select(f =>
                        new DocumentFieldDto(f.Id, f.Name, f.Value, f.Confidence, f.DataType.ToString())).ToList(),
                    document.ProcessingResult.ConfidenceScore,
                    document.ProcessingResult.RawOcrText,
                    document.ProcessingResult.AiProvider,
                    new ProcessingMetricsDto(
                        document.ProcessingResult.Metrics.OcrDurationMs,
                        document.ProcessingResult.Metrics.AiDurationMs,
                        document.ProcessingResult.Metrics.TotalDurationMs,
                        document.ProcessingResult.Metrics.TokenCount,
                        document.ProcessingResult.Metrics.EstimatedCost),
                    document.ProcessingResult.CreatedAt)
                : null,
            document.UploadedAt, document.ProcessedAt);

    private static FileType? ParseFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => FileType.Pdf,
            ".jpg" or ".jpeg" => FileType.Jpeg,
            ".png" => FileType.Png,
            ".tiff" or ".tif" => FileType.Tiff,
            ".bmp" => FileType.Bmp,
            _ => null
        };
    }
}
