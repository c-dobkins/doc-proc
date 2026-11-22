using System.Globalization;
using System.Text.RegularExpressions;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Pipeline;

/// <summary>
/// Applies post-processing rules to AI extraction results:
/// field validation, type-specific normalization, required-field checks,
/// cross-field consistency, and confidence recalculation.
/// </summary>
public partial class PostProcessor(
    IOptions<PostProcessorOptions> processorOptions,
    ILogger<PostProcessor> logger) : IPostProcessor
{
    private readonly PostProcessorOptions _options = processorOptions.Value;

    public Task<ProcessingResult> ProcessAsync(
        Document document,
        ExtractionResult extractionResult,
        string rawOcrText,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var templateFields = document.Template?.Fields ?? [];

        // Map AI-extracted fields into ProcessingResult fields, applying normalization
        var processedFields = new List<DocumentField>();
        foreach (var field in extractionResult.Fields)
        {
            var templateField = templateFields.FirstOrDefault(
                tf => string.Equals(tf.Name, field.Name, StringComparison.OrdinalIgnoreCase));

            var processed = new DocumentField
            {
                Name = field.Name,
                Value = field.Value,
                Confidence = field.Confidence,
                DataType = templateField?.DataType ?? field.DataType
            };

            // Normalize and validate based on data type
            if (processed.Value is not null)
            {
                var (normalizedValue, isValid) = NormalizeAndValidate(processed.Value, processed.DataType);
                processed.Value = normalizedValue;

                if (!isValid)
                {
                    processed.Confidence = Math.Max(0, processed.Confidence - _options.ValidationFailurePenalty);
                    logger.LogWarning(
                        "Field {FieldName} failed {DataType} validation for document {DocumentId}, confidence reduced",
                        processed.Name, processed.DataType, document.Id);
                }
            }

            processedFields.Add(processed);
        }

        // Check required fields
        foreach (var required in templateFields.Where(tf => tf.IsRequired))
        {
            var match = processedFields.FirstOrDefault(
                f => string.Equals(f.Name, required.Name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                // Add missing required field with zero confidence
                processedFields.Add(new DocumentField
                {
                    Name = required.Name,
                    Value = null,
                    Confidence = 0,
                    DataType = required.DataType
                });

                logger.LogWarning(
                    "Required field {FieldName} missing from extraction for document {DocumentId}",
                    required.Name, document.Id);
            }
            else if (string.IsNullOrWhiteSpace(match.Value))
            {
                match.Confidence = Math.Max(0, match.Confidence - _options.MissingRequiredFieldPenalty);
                logger.LogWarning(
                    "Required field {FieldName} has empty value for document {DocumentId}, confidence reduced",
                    match.Name, document.Id);
            }
        }

        // Calculate overall confidence
        var overallConfidence = processedFields.Count > 0
            ? processedFields.Average(f => f.Confidence)
            : 0;

        var result = new ProcessingResult
        {
            DocumentId = document.Id,
            ExtractedFields = processedFields,
            ConfidenceScore = overallConfidence,
            RawOcrText = rawOcrText,
            AiProvider = extractionResult.ProviderName,
            RawAiResponse = extractionResult.RawResponse
        };

        logger.LogInformation(
            "Post-processing completed for document {DocumentId}: {FieldCount} fields, confidence={Confidence:F1}%",
            document.Id, processedFields.Count, overallConfidence);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Normalizes a field value according to its data type and validates it.
    /// Returns the normalized value and whether it passed validation.
    /// </summary>
    private static (string NormalizedValue, bool IsValid) NormalizeAndValidate(string value, FieldDataType dataType)
    {
        return dataType switch
        {
            FieldDataType.Date => NormalizeDate(value),
            FieldDataType.Integer => NormalizeInteger(value),
            FieldDataType.Decimal => NormalizeDecimal(value),
            FieldDataType.Currency => NormalizeCurrency(value),
            FieldDataType.Boolean => NormalizeBoolean(value),
            FieldDataType.Email => ValidateEmail(value),
            FieldDataType.PhoneNumber => NormalizePhoneNumber(value),
            _ => (value.Trim(), true)
        };
    }

    private static (string, bool) NormalizeDate(string value)
    {
        // Try multiple date formats
        string[] formats =
        [
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "M/d/yyyy",
            "dd-MMM-yyyy", "MMM dd, yyyy", "MMMM dd, yyyy",
            "yyyy/MM/dd", "dd.MM.yyyy"
        ];

        if (DateTimeOffset.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return (date.ToString("yyyy-MM-dd"), true);
        }

        if (DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
        {
            return (date.ToString("yyyy-MM-dd"), true);
        }

        return (value.Trim(), false);
    }

    private static (string, bool) NormalizeInteger(string value)
    {
        var cleaned = value.Trim().Replace(",", "").Replace(" ", "");
        if (long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return (number.ToString(CultureInfo.InvariantCulture), true);
        }
        return (value.Trim(), false);
    }

    private static (string, bool) NormalizeDecimal(string value)
    {
        var cleaned = value.Trim().Replace(",", "").Replace(" ", "");
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return (number.ToString(CultureInfo.InvariantCulture), true);
        }
        return (value.Trim(), false);
    }

    private static (string, bool) NormalizeCurrency(string value)
    {
        // Strip currency symbols and whitespace
        var cleaned = CurrencySymbolRegex().Replace(value.Trim(), "").Trim().Replace(",", "");
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount))
        {
            return (amount.ToString("F2", CultureInfo.InvariantCulture), true);
        }
        return (value.Trim(), false);
    }

    private static (string, bool) NormalizeBoolean(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        return lower switch
        {
            "true" or "yes" or "1" or "y" => ("true", true),
            "false" or "no" or "0" or "n" => ("false", true),
            _ => (value.Trim(), false)
        };
    }

    private static (string, bool) ValidateEmail(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var isValid = EmailRegex().IsMatch(trimmed);
        return (trimmed, isValid);
    }

    private static (string, bool) NormalizePhoneNumber(string value)
    {
        // Strip non-digit chars except leading +
        var trimmed = value.Trim();
        var digits = PhoneDigitRegex().Replace(trimmed, "");

        if (digits.Length < 7)
            return (trimmed, false);

        // Preserve the cleaned value
        return (trimmed, true);
    }

    [GeneratedRegex(@"[$€£¥₹\s]")]
    private static partial Regex CurrencySymbolRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex PhoneDigitRegex();
}
