using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ocr;
using ImageMagick;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class DocumentPreProcessorTests
{
    private readonly DocumentPreProcessor _preProcessor;

    public DocumentPreProcessorTests()
    {
        var logger = Substitute.For<ILogger<DocumentPreProcessor>>();
        _preProcessor = new DocumentPreProcessor(logger);
    }

    [Fact]
    public async Task ProcessAsync_EmptyStream_ReturnsInvalid()
    {
        var document = new Document { FileName = "test.pdf", FileType = FileType.Pdf };
        using var stream = new MemoryStream();

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ValidationError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_PdfFile_ReturnsValidWithStream()
    {
        var document = new Document { FileName = "test.pdf", FileType = FileType.Pdf };
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.ValidationError);
        Assert.Single(result.PreparedPages);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public async Task ProcessAsync_PdfFile_CopiesStreamContent()
    {
        var document = new Document { FileName = "test.pdf", FileType = FileType.Pdf };
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }; // PDF header bytes
        using var stream = new MemoryStream(content);

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.True(result.IsValid);
        var preparedStream = result.PreparedPages[0];
        Assert.Equal(content.Length, preparedStream.Length);

        // Clean up
        foreach (var s in result.PreparedPages) s.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_InvalidImageFile_ReturnsInvalid()
    {
        var document = new Document { FileName = "test.jpg", FileType = FileType.Jpeg };
        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 }); // Not a valid image

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ValidationError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_PdfFile_IsNotMarkedImageBased()
    {
        var document = new Document { FileName = "test.pdf", FileType = FileType.Pdf };
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.False(result.IsImageBased);

        foreach (var s in result.PreparedPages) s.Dispose();
    }

    [Theory]
    [InlineData(FileType.Png, MagickFormat.Png)]
    [InlineData(FileType.Jpeg, MagickFormat.Jpeg)]
    [InlineData(FileType.Tiff, MagickFormat.Tiff)]
    [InlineData(FileType.Bmp, MagickFormat.Bmp)]
    public async Task ProcessAsync_ValidImage_ReturnsGrayscalePng(FileType fileType, MagickFormat format)
    {
        using var source = new MagickImage(MagickColors.Red, 40, 30);
        var document = new Document { FileName = "scan", FileType = fileType };
        using var stream = new MemoryStream(source.ToByteArray(format));

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.True(result.IsValid, result.ValidationError);
        Assert.True(result.IsImageBased);
        using var prepared = new MagickImage(result.PreparedPages[0]);
        Assert.Equal(MagickFormat.Png, prepared.Format);
        Assert.Equal(ColorSpace.Gray, prepared.ColorSpace);
        Assert.Equal(40u, prepared.Width);
        Assert.Equal(30u, prepared.Height);
    }

    [Fact]
    public async Task ProcessAsync_ImageDeclaredAsDifferentType_ReturnsInvalid()
    {
        // A PNG uploaded as .jpg must not be decoded by sniffing its content
        using var source = new MagickImage(MagickColors.Red, 10, 10);
        var document = new Document { FileName = "mislabelled.jpg", FileType = FileType.Jpeg };
        using var stream = new MemoryStream(source.ToByteArray(MagickFormat.Png));

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ProcessAsync_RotatedJpeg_IsAutoOriented()
    {
        using var source = new MagickImage(MagickColors.Red, 40, 20);
        // Like a phone photo: pixels stored landscape, EXIF says "rotate 90° to display"
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        source.SetProfile(exif);
        source.Orientation = Orientation.RightTop;
        var document = new Document { FileName = "phone.jpg", FileType = FileType.Jpeg };
        using var stream = new MemoryStream(source.ToByteArray(MagickFormat.Jpeg));

        var result = await _preProcessor.ProcessAsync(document, stream, CancellationToken.None);

        using var prepared = new MagickImage(result.PreparedPages[0]);
        Assert.Equal(20u, prepared.Width);
        Assert.Equal(40u, prepared.Height);
    }
}
