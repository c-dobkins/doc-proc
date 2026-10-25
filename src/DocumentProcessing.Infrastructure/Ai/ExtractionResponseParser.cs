using System.Text.Json;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Parses AI provider JSON responses into ExtractionResult.
/// Sets <see cref="ExtractionResult.ParsedFormat"/> to indicate which format was detected,
/// allowing callers to log or alert on unexpected format drift.
/// </summary>
public static class ExtractionResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ExtractionResult Parse(string json, string providerName, long processingTimeMs, int tokensUsed)
    {
        var result = new ExtractionResult
        {
            ProviderName = providerName,
            ProcessingTimeMs = processingTimeMs,
            TokensUsed = tokensUsed,
            RawResponse = json
        };

        try
        {
            // Strip markdown code fences if present
            var cleaned = json.Trim();
            if (cleaned.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0) cleaned = cleaned[(firstNewline + 1)..];
                if (cleaned.EndsWith("```", StringComparison.Ordinal))
                    cleaned = cleaned[..^3];
                cleaned = cleaned.Trim();
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            if (root.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
            {
                // Standard format: { "fields": [{ "name": "...", "value": "...", "confidence": N, "dataType": "..." }] }
                result.ParsedFormat = ResponseParseFormat.Structured;

                foreach (var fieldElement in fieldsElement.EnumerateArray())
                {
                    var field = new DocumentField
                    {
                        Name = fieldElement.GetProperty("name").GetString() ?? "unknown",
                        Value = fieldElement.TryGetProperty("value", out var val) && val.ValueKind != JsonValueKind.Null
                            ? val.ToString()
                            : null,
                        Confidence = fieldElement.TryGetProperty("confidence", out var conf)
                            ? conf.GetDouble()
                            : 0,
                        DataType = fieldElement.TryGetProperty("dataType", out var dt)
                            ? Enum.TryParse<FieldDataType>(dt.GetString(), ignoreCase: true, out var parsed) ? parsed : FieldDataType.String
                            : FieldDataType.String
                    };

                    result.Fields.Add(field);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Fallback flat format: { "InvoiceNumber": { "value": "INV-001", "confidence": 90 }, ... }
                // This format indicates the provider ignored the requested output schema.
                result.ParsedFormat = ResponseParseFormat.Flat;

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object) continue;

                    var fieldObj = property.Value;
                    var field = new DocumentField
                    {
                        Name = property.Name,
                        Value = fieldObj.TryGetProperty("value", out var val) && val.ValueKind != JsonValueKind.Null
                            ? val.ToString()
                            : null,
                        Confidence = fieldObj.TryGetProperty("confidence", out var conf)
                            ? conf.GetDouble()
                            : 0,
                        DataType = fieldObj.TryGetProperty("dataType", out var dt)
                            ? Enum.TryParse<FieldDataType>(dt.GetString(), ignoreCase: true, out var parsed) ? parsed : FieldDataType.String
                            : FieldDataType.String
                    };

                    result.Fields.Add(field);
                }
            }
            else
            {
                result.ParsedFormat = ResponseParseFormat.ParseFailed;
            }

            result.OverallConfidence = result.Fields.Count > 0
                ? result.Fields.Average(f => f.Confidence)
                : 0;
        }
        catch (JsonException)
        {
            result.ParsedFormat = ResponseParseFormat.ParseFailed;
            result.OverallConfidence = 0;
        }

        return result;
    }
}
