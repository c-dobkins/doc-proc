using DocumentProcessing.Infrastructure.Ocr;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class PdfPigTextExtractorTests
{
    private readonly PdfPigTextExtractor _extractor;

    public PdfPigTextExtractorTests()
    {
        var logger = Substitute.For<ILogger<PdfPigTextExtractor>>();
        _extractor = new PdfPigTextExtractor(logger);
    }

    [Fact]
    public void Extract_InvalidPdfStream_ReturnsErrorResult()
    {
        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var result = _extractor.Extract(stream);

        Assert.True(result.IsFullyScanned); // Assumed scanned on failure
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void Extract_EmptyStream_ReturnsErrorResult()
    {
        using var stream = new MemoryStream();

        var result = _extractor.Extract(stream);

        Assert.NotNull(result.Error);
    }

    [Fact]
    public void CombinePageTexts_SinglePage_ReturnsTextDirectly()
    {
        var pages = new List<PageText>
        {
            new() { PageNumber = 1, Text = "Hello World", HasTextLayer = true }
        };

        var combined = PdfPigTextExtractor.CombinePageTexts(pages);

        Assert.Equal("Hello World", combined);
    }

    [Fact]
    public void CombinePageTexts_MultiplePages_IncludesPageMarkers()
    {
        var pages = new List<PageText>
        {
            new() { PageNumber = 1, Text = "Page one content", HasTextLayer = true },
            new() { PageNumber = 2, Text = "Page two content", HasTextLayer = true }
        };

        var combined = PdfPigTextExtractor.CombinePageTexts(pages);

        Assert.Contains("--- Page 1 ---", combined);
        Assert.Contains("Page one content", combined);
        Assert.Contains("--- Page 2 ---", combined);
        Assert.Contains("Page two content", combined);
    }

    [Fact]
    public void CombinePageTexts_EmptyPages_ReturnsEmptyString()
    {
        var pages = new List<PageText>();

        var combined = PdfPigTextExtractor.CombinePageTexts(pages);

        Assert.Equal(string.Empty, combined);
    }
}
