using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Builds prompts from ExtractionTemplate by interpolating template variables.
/// Required placeholders: {ocrText}, {fieldDefinitions}, {outputFormat}
/// </summary>
public static class PromptBuilder
{
    private static readonly string[] RequiredPlaceholders = ["{ocrText}", "{fieldDefinitions}", "{outputFormat}"];

    public static string Build(string ocrText, ExtractionTemplate template)
    {
        var missing = RequiredPlaceholders
            .Where(p => !template.PromptTemplate.Contains(p, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count > 0)
            throw new ArgumentException(
                $"PromptTemplate is missing required placeholder(s): {string.Join(", ", missing)}. " +
                $"All three are required: {string.Join(", ", RequiredPlaceholders)}",
                nameof(template));

        var fieldDefinitions = BuildFieldDefinitions(template.Fields);
        var outputFormat = BuildOutputFormat(template.Fields);

        return template.PromptTemplate
            .Replace("{ocrText}", ocrText, StringComparison.OrdinalIgnoreCase)
            .Replace("{fieldDefinitions}", fieldDefinitions, StringComparison.OrdinalIgnoreCase)
            .Replace("{outputFormat}", outputFormat, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFieldDefinitions(List<TemplateField> fields)
    {
        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            sb.Append($"- {field.Name} ({field.DataType})");
            if (field.IsRequired) sb.Append(" [required]");
            if (!string.IsNullOrWhiteSpace(field.Description)) sb.Append($": {field.Description}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildOutputFormat(List<TemplateField> fields)
    {
        var fieldExamples = fields.Select(f => new Dictionary<string, object>
        {
            ["name"] = f.Name,
            ["value"] = $"<extracted {f.DataType} value>",
            ["confidence"] = 85,
            ["dataType"] = f.DataType.ToString()
        }).ToArray();

        var schema = new Dictionary<string, object> { ["fields"] = fieldExamples };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds the system prompt instructing the model to extract structured data.
    /// </summary>
    public static string BuildSystemPrompt() =>
        """
        You are a document data extraction assistant. Your task is to extract structured data from OCR text.
        
        Rules:
        1. Extract ONLY the fields specified in the field definitions.
        2. Return a JSON object with a "fields" array. Each element has: "name" (string), "value" (string or null), "confidence" (integer 0-100), "dataType" (string).
        3. If a field cannot be found, set value to null and confidence to 0.
        4. Confidence should reflect how certain you are about the extracted value (0 = not found, 100 = certain).
        5. Return ONLY valid JSON. No markdown, no explanation, no code fences.
        """;
}
