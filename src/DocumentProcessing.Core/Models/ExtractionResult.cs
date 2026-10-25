using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Core.Models;

/// <summary>
/// Result returned by an AI extraction provider.
/// </summary>
public class ExtractionResult
{
    public List<DocumentField> Fields { get; set; } = [];

    public double OverallConfidence { get; set; }

    public int TokensUsed { get; set; }

    public long ProcessingTimeMs { get; set; }

    public required string ProviderName { get; set; }

    public string? RawResponse { get; set; }

    /// <summary>
    /// Indicates which JSON format was detected in the provider response.
    /// <see cref="ResponseParseFormat.Flat"/> is a warning signal that the provider
    /// is not using the requested structured format.
    /// </summary>
    public ResponseParseFormat ParsedFormat { get; set; }
}
