using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Tests;

public class DocumentModelTests
{
    [Fact]
    public void Document_DefaultValues_AreCorrect()
    {
        var document = new Document { FileName = "test.pdf" };

        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal("test.pdf", document.FileName);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.True(document.UploadedAt <= DateTimeOffset.UtcNow);
        Assert.Null(document.ProcessedAt);
    }

    [Fact]
    public void ProcessingResult_DefaultValues_AreCorrect()
    {
        var result = new ProcessingResult { DocumentId = Guid.NewGuid() };

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Empty(result.ExtractedFields);
        Assert.NotNull(result.Metrics);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public void ExtractionTemplate_DefaultValues_AreCorrect()
    {
        var template = new ExtractionTemplate
        {
            Name = "Invoice",
            PromptTemplate = "Extract fields from: {ocr_text}"
        };

        Assert.NotEqual(Guid.Empty, template.Id);
        Assert.Equal("Invoice", template.Name);
        Assert.Equal(1, template.Version);
        Assert.Empty(template.Fields);
    }
}
