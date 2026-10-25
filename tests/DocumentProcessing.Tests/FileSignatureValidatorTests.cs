using DocumentProcessing.Api.Validators;
using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Tests;

public class FileSignatureValidatorTests
{
    [Theory]
    [InlineData(FileType.Pdf, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 })]
    [InlineData(FileType.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 })]
    [InlineData(FileType.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 })]
    [InlineData(FileType.Tiff, new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08 })]
    [InlineData(FileType.Tiff, new byte[] { 0x4D, 0x4D, 0x00, 0x2A, 0x00 })]
    [InlineData(FileType.Bmp, new byte[] { 0x42, 0x4D, 0x36, 0x00 })]
    public async Task MatchesAsync_ValidSignature_ReturnsTrue(FileType fileType, byte[] header)
    {
        Assert.True(await FileSignatureValidator.MatchesAsync(new MemoryStream(header), fileType, CancellationToken.None));
    }

    [Theory]
    [InlineData(FileType.Pdf, "<html><body>not a pdf</body></html>")]
    [InlineData(FileType.Png, "MZ executable")]
    [InlineData(FileType.Jpeg, "")]
    public async Task MatchesAsync_MismatchedOrEmptyContent_ReturnsFalse(FileType fileType, string content)
    {
        var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(content));

        Assert.False(await FileSignatureValidator.MatchesAsync(stream, fileType, CancellationToken.None));
    }
}
