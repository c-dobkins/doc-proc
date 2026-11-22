using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ocr;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class OcrEngineRouterTests
{
    private readonly IPdfTextExtractor _pdfExtractor;
    private readonly ITesseractEngine _tesseract;
    private readonly OcrEngineRouter _router;

    public OcrEngineRouterTests()
    {
        _pdfExtractor = Substitute.For<IPdfTextExtractor>();
        _tesseract = Substitute.For<ITesseractEngine>();
        _router = new OcrEngineRouter(_pdfExtractor, _tesseract, Substitute.For<ILogger<OcrEngineRouter>>());
    }

    private static Document PdfDoc() => new() { FileName = "test.pdf", FileType = FileType.Pdf };
    private static Document ImageDoc(FileType type = FileType.Jpeg) => new() { FileName = "scan.jpg", FileType = type };

    private static PdfExtractionResult DigitalPdfResult(string text = "digital text") => new()
    {
        IsFullyScanned = false,
        ScannedPageCount = 0,
        TotalPages = 1,
        Pages = [new PageText { PageNumber = 1, Text = text, HasTextLayer = true }]
    };

    private static PdfExtractionResult FullyScannedResult() => new()
    {
        IsFullyScanned = true,
        ScannedPageCount = 1,
        TotalPages = 1,
        Pages = [new PageText { PageNumber = 1, Text = "", HasTextLayer = false }]
    };

    private static PdfExtractionResult PdfPigErrorResult() => new()
    {
        IsFullyScanned = true,
        Error = "PdfPig could not open file",
        Pages = [],
        TotalPages = 0
    };

    private static PdfExtractionResult MixedPdfResult() => new()
    {
        IsFullyScanned = false,
        ScannedPageCount = 1,
        TotalPages = 2,
        Pages =
        [
            new PageText { PageNumber = 1, Text = "digital page", HasTextLayer = true },
            new PageText { PageNumber = 2, Text = "", HasTextLayer = false }
        ]
    };

    // ── Digital PDF ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DigitalPdf_UsesPdfPigEngine()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(DigitalPdfResult());
        _pdfExtractor.CombinePageTexts(Arg.Any<IReadOnlyList<PageText>>()).Returns("combined text");

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.Equal("PdfPig", result.EngineUsed);
        Assert.False(result.IsScanned);
    }

    [Fact]
    public async Task DigitalPdf_ReturnsCombinedText()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(DigitalPdfResult());
        _pdfExtractor.CombinePageTexts(Arg.Any<IReadOnlyList<PageText>>()).Returns("invoice text");

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.Equal("invoice text", result.Text);
    }

    [Fact]
    public async Task DigitalPdf_DoesNotCallTesseract()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(DigitalPdfResult());
        _pdfExtractor.CombinePageTexts(Arg.Any<IReadOnlyList<PageText>>()).Returns("text");

        await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        await _tesseract.DidNotReceive().RecognizeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _tesseract.DidNotReceive().Recognize(Arg.Any<byte[]>());
    }

    // ── Fully scanned PDF ──────────────────────────────────────────────────────

    [Fact]
    public async Task FullyScannedPdf_ReportsScannedTrue()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(FullyScannedResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.True(result.IsScanned);
    }

    [Fact]
    public async Task FullyScannedPdf_UsesTesseractEngineName()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(FullyScannedResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.Contains("Tesseract", result.EngineUsed);
    }

    // ── PdfPig error → Tesseract fallback ─────────────────────────────────────

    [Fact]
    public async Task PdfPigError_FallsBackToTesseract()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(PdfPigErrorResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.True(result.IsScanned);
        Assert.Contains("Tesseract", result.EngineUsed);
    }

    // ── Mixed PDF ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MixedPdf_IsMarkedAsScanned()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(MixedPdfResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.True(result.IsScanned);
    }

    [Fact]
    public async Task MixedPdf_UsesCombinedEngineName()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(MixedPdfResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.Contains("PdfPig", result.EngineUsed);
        Assert.Contains("Tesseract", result.EngineUsed);
    }

    [Fact]
    public async Task MixedPdf_IncludesPlaceholderForScannedPages()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(MixedPdfResult());

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.Contains("digital page", result.Text);
        Assert.Contains("Scanned page", result.Text);
    }

    [Fact]
    public async Task MixedPdf_RunsTesseractOnScannedPageImages()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(MixedPdfResult());
        _tesseract.Recognize(Arg.Any<byte[]>()).Returns(new TesseractPageResult { Text = "ocr from scan" });

        await using var pdf = new MemoryStream(BuildPdfWithImageOnPage2());

        var result = await _router.ExtractTextAsync(PdfDoc(), pdf, CancellationToken.None);

        _tesseract.Received(1).Recognize(Arg.Any<byte[]>());
        Assert.Contains("digital page", result.Text);
        Assert.Contains("ocr from scan", result.Text);
        Assert.DoesNotContain("Scanned page", result.Text);
    }

    private static byte[] BuildPdfWithImageOnPage2()
    {
        using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.White, 20, 20);
        var png = image.ToByteArray(ImageMagick.MagickFormat.Png24); // 8-bit RGB: PdfPig can embed it

        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4)
            .AddText("digital page", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4)
            .AddPng(png, new UglyToad.PdfPig.Core.PdfRectangle(0, 0, 595, 842));

        return builder.Build();
    }

    // ── Image documents ───────────────────────────────────────────────────────

    [Fact]
    public async Task ImageDocument_UsesTesseractEngine()
    {
        _tesseract.RecognizeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new TesseractPageResult { Text = "scanned text" });

        var result = await _router.ExtractTextAsync(ImageDoc(), Stream.Null, CancellationToken.None);

        Assert.Equal("Tesseract", result.EngineUsed);
        Assert.True(result.IsScanned);
    }

    [Fact]
    public async Task ImageDocument_DoesNotCallPdfExtractor()
    {
        _tesseract.RecognizeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new TesseractPageResult { Text = "text" });

        await _router.ExtractTextAsync(ImageDoc(), Stream.Null, CancellationToken.None);

        _pdfExtractor.DidNotReceive().Extract(Arg.Any<Stream>());
    }

    [Theory]
    [InlineData(FileType.Jpeg)]
    [InlineData(FileType.Png)]
    [InlineData(FileType.Tiff)]
    [InlineData(FileType.Bmp)]
    public async Task AllImageTypes_RouteToTesseract(FileType fileType)
    {
        _tesseract.RecognizeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new TesseractPageResult { Text = "text" });

        var result = await _router.ExtractTextAsync(ImageDoc(fileType), Stream.Null, CancellationToken.None);

        Assert.Equal("Tesseract", result.EngineUsed);
    }

    // ── Timing ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AlwaysPopulatesProcessingTimeMs()
    {
        _pdfExtractor.Extract(Arg.Any<Stream>()).Returns(DigitalPdfResult());
        _pdfExtractor.CombinePageTexts(Arg.Any<IReadOnlyList<PageText>>()).Returns("text");

        var result = await _router.ExtractTextAsync(PdfDoc(), Stream.Null, CancellationToken.None);

        Assert.True(result.ProcessingTimeMs >= 0);
    }
}
