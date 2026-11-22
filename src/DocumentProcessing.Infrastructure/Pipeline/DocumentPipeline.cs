using System.Diagnostics;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Pipeline;

/// <summary>
/// Orchestrates the full document processing pipeline:
/// Ingestion → Pre-processing → OCR → AI Extraction → Post-processing → Storage
/// </summary>
public class DocumentPipeline(
    IDocumentRepository documentRepository,
    IBlobStorage blobStorage,
    IPreProcessor preProcessor,
    IOcrEngine ocrEngine,
    IAiProviderFactory aiProviderFactory,
    IPostProcessor postProcessor,
    IResultStore resultStore,
    IOptions<ProcessingOptions> processingOptions,
    ILogger<DocumentPipeline> logger) : IDocumentPipeline
{
    private readonly ProcessingOptions _options = processingOptions.Value;

    public async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var document = await documentRepository.GetByIdAsync(documentId, ct);

        if (document is null)
        {
            logger.LogError("Document {DocumentId} not found", documentId);
            return;
        }

        try
        {
            document.Status = DocumentStatus.Processing;
            await documentRepository.UpdateAsync(document, ct);

            logger.LogInformation("Processing document {DocumentId} ({FileName})", documentId, document.FileName);

            // Step 1: Read file from blob storage
            await using var fileStream = await blobStorage.GetAsync(document.StoragePath!, ct);

            // Step 2: Pre-process (validate file type, copy to MemoryStream)
            var prepResult = await preProcessor.ProcessAsync(document, fileStream, ct);

            if (!prepResult.IsValid)
            {
                logger.LogWarning("Pre-processing failed for document {DocumentId}: {Error}",
                    documentId, prepResult.ValidationError);
                document.Status = DocumentStatus.Failed;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await documentRepository.UpdateAsync(document, ct);
                return;
            }

            await using var preparedStream = prepResult.PreparedPages[0];

            // Step 3: OCR text extraction
            var ocrResult = await ocrEngine.ExtractTextAsync(document, preparedStream, ct);

            logger.LogInformation(
                "OCR completed for {DocumentId}: {PageCount} pages, engine={Engine}, scanned={IsScanned}",
                documentId, ocrResult.PageCount, ocrResult.EngineUsed, ocrResult.IsScanned);

            // Step 4: AI extraction
            var template = document.Template;

            if (template is null)
            {
                logger.LogWarning("No template assigned to document {DocumentId}, skipping AI extraction", documentId);

                // Still save OCR results so the user can see extracted text
                totalStopwatch.Stop();
                var ocrOnlyResult = new ProcessingResult
                {
                    DocumentId = document.Id,
                    ExtractedFields = [],
                    ConfidenceScore = 0,
                    RawOcrText = ocrResult.Text,
                    AiProvider = null,
                    Metrics = new ProcessingMetrics
                    {
                        OcrDurationMs = ocrResult.ProcessingTimeMs,
                        TotalDurationMs = totalStopwatch.ElapsedMilliseconds
                    }
                };

                await resultStore.SaveResultAsync(ocrOnlyResult, ct);

                document.Status = DocumentStatus.NeedsReview;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await documentRepository.UpdateAsync(document, ct);

                logger.LogInformation(
                    "Document {DocumentId} OCR complete (no template). OCR chars={OcrChars}, Duration={Duration}ms",
                    documentId, ocrResult.Text.Length, totalStopwatch.ElapsedMilliseconds);
                return;
            }

            var aiProvider = await aiProviderFactory.GetDefaultProviderAsync(ct);
            var extractionResult = await aiProvider.ExtractAsync(ocrResult.Text, template, ct);

            logger.LogInformation(
                "AI extraction completed for {DocumentId}: provider={Provider}, confidence={Confidence:F1}%, tokens={Tokens}",
                documentId, extractionResult.ProviderName, extractionResult.OverallConfidence, extractionResult.TokensUsed);

            // Step 5: Post-processing
            var processingResult = await postProcessor.ProcessAsync(document, extractionResult, ocrResult.Text, ct);

            // Set metrics
            totalStopwatch.Stop();
            processingResult.Metrics.OcrDurationMs = ocrResult.ProcessingTimeMs;
            processingResult.Metrics.AiDurationMs = extractionResult.ProcessingTimeMs;
            processingResult.Metrics.TotalDurationMs = totalStopwatch.ElapsedMilliseconds;
            processingResult.Metrics.TokenCount = extractionResult.TokensUsed;

            // Step 6: Save results
            await resultStore.SaveResultAsync(processingResult, ct);

            // Step 7: Route by confidence: auto-approve, otherwise a human reviews it
            document.Status = processingResult.ConfidenceScore >= _options.AutoApproveThreshold
                ? DocumentStatus.Completed
                : DocumentStatus.NeedsReview;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await documentRepository.UpdateAsync(document, ct);

            logger.LogInformation(
                "Document {DocumentId} processed successfully. Status={Status}, Confidence={Confidence:F1}%, Duration={Duration}ms",
                documentId, document.Status, processingResult.ConfidenceScore, totalStopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {DocumentId}", documentId);
            document.Status = DocumentStatus.Failed;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await documentRepository.UpdateAsync(document, ct);
        }
    }
}
