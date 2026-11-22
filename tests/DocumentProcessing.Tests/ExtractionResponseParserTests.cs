using DocumentProcessing.Infrastructure.Ai;

namespace DocumentProcessing.Tests;

public class ExtractionResponseParserTests
{
    [Fact]
    public void Parse_ValidJsonResponse_ReturnsFieldsWithConfidence()
    {
        var json = """
        {
            "fields": [
                { "name": "InvoiceNumber", "value": "INV-001", "confidence": 95, "dataType": "String" },
                { "name": "TotalAmount", "value": "1250.00", "confidence": 88, "dataType": "Currency" },
                { "name": "InvoiceDate", "value": "2025-01-15", "confidence": 92, "dataType": "Date" }
            ]
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "TestProvider", 500, 100);

        Assert.Equal(3, result.Fields.Count);
        Assert.Equal("INV-001", result.Fields[0].Value);
        Assert.Equal(95, result.Fields[0].Confidence);
        Assert.Equal("TestProvider", result.ProviderName);
        Assert.Equal(500, result.ProcessingTimeMs);
        Assert.Equal(100, result.TokensUsed);
    }

    [Fact]
    public void Parse_CalculatesOverallConfidence()
    {
        var json = """
        {
            "fields": [
                { "name": "Field1", "value": "A", "confidence": 90 },
                { "name": "Field2", "value": "B", "confidence": 80 }
            ]
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Equal(85, result.OverallConfidence);
    }

    [Fact]
    public void Parse_NullFieldValue_SetsValueToNull()
    {
        var json = """
        {
            "fields": [
                { "name": "MissingField", "value": null, "confidence": 0 }
            ]
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Single(result.Fields);
        Assert.Null(result.Fields[0].Value);
        Assert.Equal(0, result.Fields[0].Confidence);
    }

    [Fact]
    public void Parse_StripsMarkdownCodeFences()
    {
        var json = """
        ```json
        {
            "fields": [
                { "name": "Test", "value": "Value", "confidence": 90 }
            ]
        }
        ```
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Single(result.Fields);
        Assert.Equal("Value", result.Fields[0].Value);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyResultWithZeroConfidence()
    {
        var json = "This is not JSON at all";

        var result = ExtractionResponseParser.Parse(json, "Test", 100, 50);

        Assert.Empty(result.Fields);
        Assert.Equal(0, result.OverallConfidence);
        Assert.Equal("Test", result.ProviderName);
        Assert.Equal(100, result.ProcessingTimeMs);
        Assert.Equal(50, result.TokensUsed);
    }

    [Fact]
    public void Parse_EmptyFieldsArray_ReturnsZeroConfidence()
    {
        var json = """{ "fields": [] }""";

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Empty(result.Fields);
        Assert.Equal(0, result.OverallConfidence);
    }

    [Fact]
    public void Parse_ParsesDataTypeEnum()
    {
        var json = """
        {
            "fields": [
                { "name": "Amount", "value": "99.99", "confidence": 85, "dataType": "Currency" },
                { "name": "Date", "value": "2025-01-01", "confidence": 90, "dataType": "Date" }
            ]
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Equal(Core.Enums.FieldDataType.Currency, result.Fields[0].DataType);
        Assert.Equal(Core.Enums.FieldDataType.Date, result.Fields[1].DataType);
    }

    [Fact]
    public void Parse_UnknownDataType_DefaultsToString()
    {
        var json = """
        {
            "fields": [
                { "name": "Field", "value": "test", "confidence": 80, "dataType": "UnknownType" }
            ]
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Equal(Core.Enums.FieldDataType.String, result.Fields[0].DataType);
    }

    [Fact]
    public void Parse_FlatObjectFormat_ExtractsFieldsByPropertyName()
    {
        var json = """
        {
            "InvoiceNumber": { "value": "INV-001", "confidence": 95 },
            "TotalAmount": { "value": "1250.00", "confidence": 88, "dataType": "Currency" }
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 200, 50);

        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("InvoiceNumber", result.Fields[0].Name);
        Assert.Equal("INV-001", result.Fields[0].Value);
        Assert.Equal(95, result.Fields[0].Confidence);
        Assert.Equal("TotalAmount", result.Fields[1].Name);
        Assert.Equal("1250.00", result.Fields[1].Value);
        Assert.Equal(Core.Enums.FieldDataType.Currency, result.Fields[1].DataType);
    }

    [Fact]
    public void Parse_FlatObjectFormat_DefaultsDataTypeToString()
    {
        var json = """
        {
            "Name": { "value": "John Doe", "confidence": 90 }
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Single(result.Fields);
        Assert.Equal(Core.Enums.FieldDataType.String, result.Fields[0].DataType);
    }

    [Fact]
    public void Parse_FieldsArrayPreferredOverFlatFormat()
    {
        // If "fields" array exists, flat properties are ignored
        var json = """
        {
            "fields": [
                { "name": "A", "value": "1", "confidence": 80 }
            ],
            "ExtraField": { "value": "ignored", "confidence": 50 }
        }
        """;

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Single(result.Fields);
        Assert.Equal("A", result.Fields[0].Name);
    }

    // ── ParsedFormat ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_StructuredFormat_SetsParsedFormatToStructured()
    {
        var json = """{ "fields": [{ "name": "X", "value": "Y", "confidence": 80 }] }""";

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Equal(Core.Enums.ResponseParseFormat.Structured, result.ParsedFormat);
    }

    [Fact]
    public void Parse_FlatObjectFormat_SetsParsedFormatToFlat()
    {
        var json = """{ "InvoiceNumber": { "value": "INV-001", "confidence": 90 } }""";

        var result = ExtractionResponseParser.Parse(json, "Test", 0, 0);

        Assert.Equal(Core.Enums.ResponseParseFormat.Flat, result.ParsedFormat);
    }

    [Fact]
    public void Parse_InvalidJson_SetsParsedFormatToParseFailedAndZeroConfidence()
    {
        var result = ExtractionResponseParser.Parse("this is not json", "Test", 0, 0);

        Assert.Equal(Core.Enums.ResponseParseFormat.ParseFailed, result.ParsedFormat);
        Assert.Equal(0, result.OverallConfidence);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void Parse_EmptyFieldsArray_SetsParsedFormatToStructured()
    {
        var result = ExtractionResponseParser.Parse("""{ "fields": [] }""", "Test", 0, 0);

        Assert.Equal(Core.Enums.ResponseParseFormat.Structured, result.ParsedFormat);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void Parse_PreservesRawResponseOnParseFailure()
    {
        var rawJson = "bad json { broken";

        var result = ExtractionResponseParser.Parse(rawJson, "Test", 0, 0);

        Assert.Equal(rawJson, result.RawResponse);
        Assert.Equal(Core.Enums.ResponseParseFormat.ParseFailed, result.ParsedFormat);
    }
}
