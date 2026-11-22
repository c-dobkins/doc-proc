namespace DocumentProcessing.Core.Interfaces.Pipeline;

/// <summary>
/// Selects the AI provider to use: the configured default, or the first available one in
/// <c>AiProviders:FallbackOrder</c>.
/// </summary>
public interface IAiProviderFactory
{
    Task<IAiExtractionProvider> GetDefaultProviderAsync(CancellationToken ct);
}
