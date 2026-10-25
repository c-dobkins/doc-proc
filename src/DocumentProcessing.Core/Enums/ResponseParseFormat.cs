namespace DocumentProcessing.Core.Enums;

/// <summary>
/// Indicates which JSON format the ExtractionResponseParser detected in an AI provider response.
/// Callers can use this to detect unexpected format drift across providers.
/// </summary>
public enum ResponseParseFormat
{
    /// <summary>Standard structured format: { "fields": [{ "name": ..., "value": ..., "confidence": N }] }</summary>
    Structured,

    /// <summary>Flat object format: { "FieldName": { "value": ..., "confidence": N } }</summary>
    Flat,

    /// <summary>Response could not be parsed as valid JSON.</summary>
    ParseFailed
}
