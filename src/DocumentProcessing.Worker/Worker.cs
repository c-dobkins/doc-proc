using System.Collections.Concurrent;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace DocumentProcessing.Worker;

/// <summary>
/// Background service that dequeues documents from the Channel and processes them
/// through the pipeline. Per ADR-0004: uses Channel&lt;T&gt; with configurable parallelism.
/// Sends real-time status updates via IProcessingNotifier.
/// </summary>
public class DocumentProcessingWorker(
    ChannelReader<Guid> channelReader,
    ChannelWriter<Guid> channelWriter,
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessingOptions> options,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    private readonly int _maxConcurrency = Math.Max(1, options.Value.MaxConcurrency);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document processing worker started with max concurrency {MaxConcurrency}", _maxConcurrency);

        // Start the consumer loop before recovery so the channel isn't blocked
        // when stale documents are written back onto it.
        var consumerTask = ConsumeChannelAsync(stoppingToken);

        await RecoverStaleDocumentsAsync(stoppingToken);

        await consumerTask;
    }

    private async Task ConsumeChannelAsync(CancellationToken stoppingToken)
    {
        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var inFlight = new ConcurrentDictionary<Task, byte>();

        try
        {
            await foreach (var documentId in channelReader.ReadAllAsync(stoppingToken))
            {
                await semaphore.WaitAsync(stoppingToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessDocumentAsync(documentId, stoppingToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                inFlight.TryAdd(task, 0);
                _ = task.ContinueWith(t => inFlight.TryRemove(t, out _), TaskScheduler.Default);
            }
        }
        finally
        {
            // Let in-flight documents observe cancellation and finish before the host stops.
            // Anything interrupted stays in Processing and is re-enqueued on the next start.
            await Task.WhenAll(inFlight.Keys);
        }
    }

    private async Task ProcessDocumentAsync(Guid documentId, CancellationToken ct)
    {
        logger.LogInformation("Starting processing for document {DocumentId}", documentId);

        using var scope = scopeFactory.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDocumentPipeline>();
        var notifier = scope.ServiceProvider.GetRequiredService<IProcessingNotifier>();

        try
        {
            await notifier.NotifyStatusChangedAsync(documentId, "Processing", ct);

            await pipeline.ProcessAsync(documentId, ct);

            // Read final status after pipeline completes
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var document = await documentRepository.GetByIdAsync(documentId, ct);
            var finalStatus = document?.Status.ToString() ?? "Completed";

            await notifier.NotifyStatusChangedAsync(documentId, finalStatus, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("Processing of document {DocumentId} interrupted by shutdown; it will be recovered on restart", documentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error processing document {DocumentId}", documentId);

            try { await notifier.NotifyStatusChangedAsync(documentId, "Failed", ct); }
            catch (Exception notifyEx) { logger.LogWarning(notifyEx, "Failed to send status notification for document {DocumentId}", documentId); }
        }
    }

    private async Task RecoverStaleDocumentsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var staleDocuments = await documentRepository.GetAllAsync(1, 1000, DocumentStatus.Processing, ct);

        if (staleDocuments.Count > 0)
        {
            logger.LogWarning("Found {Count} stale documents in Processing status, re-enqueuing", staleDocuments.Count);

            foreach (var doc in staleDocuments)
            {
                doc.Status = DocumentStatus.Uploaded;
                await documentRepository.UpdateAsync(doc, ct);
                await channelWriter.WriteAsync(doc.Id, ct);
            }
        }
    }
}
