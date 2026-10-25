using DocumentProcessing.Core.Interfaces.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Selects the configured default AI provider, falling back through FallbackOrder when it is unavailable.
/// </summary>
public class AiProviderFactory(
    IEnumerable<IAiExtractionProvider> providers,
    IOptions<AiProviderOptions> options,
    ILogger<AiProviderFactory> logger) : IAiProviderFactory
{
    private readonly Dictionary<string, IAiExtractionProvider> _providers =
        providers.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

    public async Task<IAiExtractionProvider> GetDefaultProviderAsync(CancellationToken ct)
    {
        var candidates = new[] { options.Value.DefaultProvider }
            .Concat(options.Value.FallbackOrder)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in candidates)
        {
            if (!_providers.TryGetValue(name, out var provider))
                continue;

            if (await provider.IsAvailableAsync(ct))
            {
                if (!string.Equals(name, options.Value.DefaultProvider, StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning("Default provider '{Default}' unavailable; using fallback '{Fallback}'",
                        options.Value.DefaultProvider, name);
                else
                    logger.LogDebug("Using default AI provider: {Provider}", name);

                return provider;
            }

            logger.LogWarning("AI provider '{Provider}' reported unavailable; trying next in FallbackOrder", name);
        }

        throw new InvalidOperationException(
            $"No AI provider is available. Tried: {string.Join(", ", candidates)}");
    }
}
