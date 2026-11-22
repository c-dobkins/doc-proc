namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Orchestrates the full document processing pipeline.
/// </summary>
public interface IDocumentPipeline
{
    Task ProcessAsync(Guid documentId, CancellationToken ct);
}
