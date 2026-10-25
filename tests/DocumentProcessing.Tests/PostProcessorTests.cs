using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class PostProcessorTests
{
    private readonly PostProcessor _sut;

    public PostProcessorTests()
    {
        var logger = Substitute.For<ILogger<PostProcessor>>();
        var options = Options.Create(new PostProcessorOptions());
        _sut = new PostProcessor(options, logger);
    }

    private static Document CreateDocument(ExtractionTemplate? template = null) => new()
    {
        FileName = "test.pdf",
        Template = template
    };

    private static ExtractionTemplate CreateTemplate(params TemplateField[] fields) => new()
    {
        Name = "TestTemplate",
        PromptTemplate = "{ocrText}",
        Fields = [.. fields]
    };

    private static ExtractionResult CreateExtractionResult(params DocumentField[] fields) => new()
    {
        ProviderName = "TestProvider",
        Fields = [.. fields],
        OverallConfidence = fields.Length > 0 ? fields.Average(f => f.Confidence) : 0
    };

    #region Basic Processing

    [Fact]
    public async Task ProcessAsync_MapsFieldsToProcessingResult()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Name", DataType = FieldDataType.String, IsRequired = false });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Name", Value = "John", Confidence = 90, DataType = FieldDataType.String });

        var result = await _sut.ProcessAsync(document, extraction, "raw text", CancellationToken.None);

        Assert.Single(result.ExtractedFields);
        Assert.Equal("John", result.ExtractedFields[0].Value);
        Assert.Equal(document.Id, result.DocumentId);
        Assert.Equal("raw text", result.RawOcrText);
        Assert.Equal("TestProvider", result.AiProvider);
    }

    [Fact]
    public async Task ProcessAsync_CalculatesOverallConfidence()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "A", DataType = FieldDataType.String },
            new TemplateField { Name = "B", DataType = FieldDataType.String });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "A", Value = "X", Confidence = 80, DataType = FieldDataType.String },
            new DocumentField { Name = "B", Value = "Y", Confidence = 100, DataType = FieldDataType.String });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(90, result.ConfidenceScore);
    }

    [Fact]
    public async Task ProcessAsync_EmptyFields_ReturnsZeroConfidence()
    {
        var document = CreateDocument();
        var extraction = CreateExtractionResult();

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(0, result.ConfidenceScore);
        Assert.Empty(result.ExtractedFields);
    }

    #endregion

    #region Required Fields

    [Fact]
    public async Task ProcessAsync_MissingRequiredField_AddedWithZeroConfidence()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "InvoiceNumber", DataType = FieldDataType.String, IsRequired = true });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(); // no fields returned

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Single(result.ExtractedFields);
        Assert.Equal("InvoiceNumber", result.ExtractedFields[0].Name);
        Assert.Null(result.ExtractedFields[0].Value);
        Assert.Equal(0, result.ExtractedFields[0].Confidence);
    }

    [Fact]
    public async Task ProcessAsync_EmptyRequiredField_ConfidenceReduced()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Amount", DataType = FieldDataType.Currency, IsRequired = true });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Amount", Value = "", Confidence = 80, DataType = FieldDataType.Currency });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.True(result.ExtractedFields[0].Confidence < 80);
    }

    #endregion

    #region Date Normalization

    [Theory]
    [InlineData("01/15/2025", "2025-01-15")]
    [InlineData("2025-01-15", "2025-01-15")]
    [InlineData("Jan 15, 2025", "2025-01-15")]
    [InlineData("15-Jan-2025", "2025-01-15")]
    public async Task ProcessAsync_NormalizesDateFormats(string input, string expected)
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Date", DataType = FieldDataType.Date });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Date", Value = input, Confidence = 90, DataType = FieldDataType.Date });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(expected, result.ExtractedFields[0].Value);
    }

    [Fact]
    public async Task ProcessAsync_InvalidDate_ConfidenceReduced()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Date", DataType = FieldDataType.Date });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Date", Value = "not-a-date", Confidence = 90, DataType = FieldDataType.Date });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.True(result.ExtractedFields[0].Confidence < 90);
    }

    #endregion

    #region Currency Normalization

    [Theory]
    [InlineData("$1,250.50", "1250.50")]
    [InlineData("€99.99", "99.99")]
    [InlineData("£1000", "1000.00")]
    [InlineData("1234.56", "1234.56")]
    public async Task ProcessAsync_NormalizesCurrency(string input, string expected)
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Amount", DataType = FieldDataType.Currency });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Amount", Value = input, Confidence = 95, DataType = FieldDataType.Currency });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(expected, result.ExtractedFields[0].Value);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCurrency_ConfidenceReduced()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Amount", DataType = FieldDataType.Currency });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Amount", Value = "abc", Confidence = 90, DataType = FieldDataType.Currency });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.True(result.ExtractedFields[0].Confidence < 90);
    }

    #endregion

    #region Integer/Decimal Normalization

    [Theory]
    [InlineData("1,000", "1000")]
    [InlineData("42", "42")]
    public async Task ProcessAsync_NormalizesIntegers(string input, string expected)
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Qty", DataType = FieldDataType.Integer });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Qty", Value = input, Confidence = 95, DataType = FieldDataType.Integer });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(expected, result.ExtractedFields[0].Value);
    }

    [Fact]
    public async Task ProcessAsync_NormalizesDecimals()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Weight", DataType = FieldDataType.Decimal });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Weight", Value = "1,234.56", Confidence = 90, DataType = FieldDataType.Decimal });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal("1234.56", result.ExtractedFields[0].Value);
    }

    #endregion

    #region Boolean Normalization

    [Theory]
    [InlineData("Yes", "true")]
    [InlineData("no", "false")]
    [InlineData("1", "true")]
    [InlineData("0", "false")]
    [InlineData("true", "true")]
    [InlineData("Y", "true")]
    public async Task ProcessAsync_NormalizesBooleans(string input, string expected)
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Active", DataType = FieldDataType.Boolean });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Active", Value = input, Confidence = 95, DataType = FieldDataType.Boolean });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(expected, result.ExtractedFields[0].Value);
    }

    #endregion

    #region Email Validation

    [Fact]
    public async Task ProcessAsync_ValidEmail_PassesValidation()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Email", DataType = FieldDataType.Email });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Email", Value = "test@example.com", Confidence = 90, DataType = FieldDataType.Email });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(90, result.ExtractedFields[0].Confidence);
        Assert.Equal("test@example.com", result.ExtractedFields[0].Value);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEmail_ConfidenceReduced()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Email", DataType = FieldDataType.Email });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Email", Value = "not-an-email", Confidence = 90, DataType = FieldDataType.Email });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.True(result.ExtractedFields[0].Confidence < 90);
    }

    #endregion

    #region Phone Number Validation

    [Fact]
    public async Task ProcessAsync_ValidPhoneNumber_Passes()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Phone", DataType = FieldDataType.PhoneNumber });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Phone", Value = "+1-555-123-4567", Confidence = 85, DataType = FieldDataType.PhoneNumber });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(85, result.ExtractedFields[0].Confidence);
    }

    [Fact]
    public async Task ProcessAsync_TooShortPhoneNumber_ConfidenceReduced()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Phone", DataType = FieldDataType.PhoneNumber });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Phone", Value = "123", Confidence = 85, DataType = FieldDataType.PhoneNumber });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.True(result.ExtractedFields[0].Confidence < 85);
    }

    #endregion

    #region Null Values

    [Fact]
    public async Task ProcessAsync_NullFieldValue_SkipsNormalization()
    {
        var template = CreateTemplate(
            new TemplateField { Name = "Notes", DataType = FieldDataType.String });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Notes", Value = null, Confidence = 0, DataType = FieldDataType.String });

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Single(result.ExtractedFields);
        Assert.Null(result.ExtractedFields[0].Value);
        Assert.Equal(0, result.ExtractedFields[0].Confidence);
    }

    #endregion

    #region Configurable Penalties

    [Fact]
    public async Task ProcessAsync_CustomValidationFailurePenalty_AppliesConfiguredValue()
    {
        var options = Options.Create(new PostProcessorOptions { ValidationFailurePenalty = 30.0, MissingRequiredFieldPenalty = 20.0 });
        var sut = new PostProcessor(options, Substitute.For<ILogger<PostProcessor>>());
        var template = CreateTemplate(new TemplateField { Name = "Date", DataType = FieldDataType.Date });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Date", Value = "not-a-date", Confidence = 80, DataType = FieldDataType.Date });

        var result = await sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(50, result.ExtractedFields[0].Confidence); // 80 - 30 = 50
    }

    [Fact]
    public async Task ProcessAsync_CustomMissingRequiredPenalty_AppliesConfiguredValue()
    {
        var options = Options.Create(new PostProcessorOptions { MissingRequiredFieldPenalty = 10.0, ValidationFailurePenalty = 15.0 });
        var sut = new PostProcessor(options, Substitute.For<ILogger<PostProcessor>>());
        // Use String type so normalization passes (only the required-field penalty fires)
        var template = CreateTemplate(new TemplateField { Name = "Name", DataType = FieldDataType.String, IsRequired = true });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Name", Value = "  ", Confidence = 50, DataType = FieldDataType.String });

        var result = await sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(40, result.ExtractedFields[0].Confidence); // 50 - 10 = 40
    }

    [Fact]
    public async Task ProcessAsync_PenaltyLargerThanConfidence_ClampsToZero()
    {
        var options = Options.Create(new PostProcessorOptions { ValidationFailurePenalty = 50.0, MissingRequiredFieldPenalty = 20.0 });
        var sut = new PostProcessor(options, Substitute.For<ILogger<PostProcessor>>());
        var template = CreateTemplate(new TemplateField { Name = "Date", DataType = FieldDataType.Date });
        var document = CreateDocument(template);
        var extraction = CreateExtractionResult(
            new DocumentField { Name = "Date", Value = "not-a-date", Confidence = 30, DataType = FieldDataType.Date });

        var result = await sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Equal(0, result.ExtractedFields[0].Confidence); // 30 - 50 clamped to 0
    }

    #endregion

    #region RawAiResponse preservation

    [Fact]
    public async Task ProcessAsync_PreservesRawAiResponseFromExtractionResult()
    {
        var template = CreateTemplate(new TemplateField { Name = "Name", DataType = FieldDataType.String });
        var document = CreateDocument(template);
        var extraction = new ExtractionResult
        {
            ProviderName = "TestProvider",
            Fields = [new DocumentField { Name = "Name", Value = "Acme", Confidence = 90, DataType = FieldDataType.String }],
            RawResponse = """{"fields":[{"name":"Name","value":"Acme","confidence":90}]}"""
        };

        var result = await _sut.ProcessAsync(document, extraction, "ocr text", CancellationToken.None);

        Assert.Equal(extraction.RawResponse, result.RawAiResponse);
    }

    [Fact]
    public async Task ProcessAsync_NullRawResponse_SetsRawAiResponseToNull()
    {
        var document = CreateDocument();
        var extraction = CreateExtractionResult(); // RawResponse is null by default

        var result = await _sut.ProcessAsync(document, extraction, "", CancellationToken.None);

        Assert.Null(result.RawAiResponse);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task ProcessAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var document = CreateDocument();
        var extraction = CreateExtractionResult();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ProcessAsync(document, extraction, "", cts.Token));
    }

    #endregion
}
