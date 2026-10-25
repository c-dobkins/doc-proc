using DocumentProcessing.Core.Enums;
using ImageMagick;

namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Safe image decoding settings for Magick.NET.
/// Uploaded images are always decoded with the coder matching their (signature-checked) file type,
/// never by content sniffing, so a crafted file can't reach ImageMagick's script-like coders
/// (MVG, MSL, SVG, ...). Global resource limits bound memory and pixel dimensions.
/// </summary>
internal static class ImageDecoding
{
    private const uint MaxDimension = 20_000;

    static ImageDecoding()
    {
        ResourceLimits.Width = MaxDimension;
        ResourceLimits.Height = MaxDimension;
        ResourceLimits.Area = 200_000_000;           // ~200 megapixels
        ResourceLimits.Memory = 512UL * 1024 * 1024;
    }

    public static MagickReadSettings ReadSettingsFor(FileType fileType) => new()
    {
        Format = fileType switch
        {
            FileType.Jpeg => MagickFormat.Jpeg,
            FileType.Png => MagickFormat.Png,
            FileType.Tiff => MagickFormat.Tiff,
            FileType.Bmp => MagickFormat.Bmp,
            _ => throw new ArgumentOutOfRangeException(nameof(fileType), fileType, "Not an image file type")
        }
    };
}
