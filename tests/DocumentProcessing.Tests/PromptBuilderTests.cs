using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ai;

namespace DocumentProcessing.Tests;

public class PromptBuilderTests
{
    private static ExtractionTemplate CreateTemplate(string promptTemplate = "{ocrText}\n{fieldDefinitions}\n{outputFormat}") => new()
    {
        Name = "Invoice",
        PromptTemplate = promptTemplate,
        Fields =
        [
            new TemplateField { Name = "InvoiceNumber", DataType = FieldDataType.String, IsRequired = true, Description = "The invoice number" },
            new TemplateField { Name = "TotalAmount", DataType = FieldDataType.Currency, IsRequired = true, Description = "Total amount due" },
            new TemplateField { Name = "InvoiceDate", DataType = FieldDataType.Date, IsRequired = false }
        ]
    };

    [Fact]
    public void Build_SubstitutesOcrText()
    {
        var template = CreateTemplate("[OCR:{ocrText}]\n{fieldDefinitions}\n{outputFormat}");
        var result = PromptBuilder.Build("Sample OCR text", template);

        Assert.Contains("Sample OCR text", result);
        Assert.StartsWith("[OCR:Sample OCR text]", result);
    }

    [Fact]
    public void Build_SubstitutesFieldDefinitions()
    {
        var template = CreateTemplate("{ocrText}\n---\n{fieldDefinitions}\n---\n{outputFormat}");
        var result = PromptBuilder.Build("text", template);

        Assert.Contains("InvoiceNumber (String) [required]: The invoice number", result);
        Assert.Contains("TotalAmount (Currency) [required]: Total amount due", result);
        Assert.Contains("InvoiceDate (Date)", result);
        Assert.DoesNotContain("[required]", result.Split('\n').Last(l => l.Contains("InvoiceDate")));
    }

    [Fact]
    public void Build_SubstitutesOutputFormat()
    {
        var template = CreateTemplate("{ocrText}\n{fieldDefinitions}\n{outputFormat}");
        var result = PromptBuilder.Build("text", template);

        Assert.Contains("\"fields\"", result);
        Assert.Contains("\"name\"", result);
        Assert.Contains("InvoiceNumber", result);
        Assert.Contains("TotalAmount", result);
        Assert.Contains("confidence", result);
        Assert.Contains("dataType", result);
    }

    [Fact]
    public void Build_IsCaseInsensitive()
    {
        var template = CreateTemplate("{OCRTEXT}|{FIELDDEFINITIONS}|{OUTPUTFORMAT}");
        var result = PromptBuilder.Build("hello", template);

        Assert.Contains("hello", result);
        Assert.Contains("InvoiceNumber", result);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsExtractionInstructions()
    {
        var prompt = PromptBuilder.BuildSystemPrompt();

        Assert.Contains("fields", prompt);
        Assert.Contains("confidence", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void Build_MissingOcrTextPlaceholder_ThrowsArgumentException()
    {
        var template = CreateTemplate("{fieldDefinitions}\n{outputFormat}");

        var ex = Assert.Throws<ArgumentException>(() => PromptBuilder.Build("text", template));

        Assert.Contains("{ocrText}", ex.Message);
    }

    [Fact]
    public void Build_MissingAllPlaceholders_ThrowsArgumentExceptionListingAll()
    {
        var template = CreateTemplate("no placeholders here");

        var ex = Assert.Throws<ArgumentException>(() => PromptBuilder.Build("text", template));

        Assert.Contains("{ocrText}", ex.Message);
        Assert.Contains("{fieldDefinitions}", ex.Message);
        Assert.Contains("{outputFormat}", ex.Message);
    }

    [Fact]
    public void Build_AllPlaceholdersPresent_DoesNotThrow()
    {
        var template = CreateTemplate("{ocrText}\n{fieldDefinitions}\n{outputFormat}");

        var ex = Record.Exception(() => PromptBuilder.Build("text", template));

        Assert.Null(ex);
    }
}
