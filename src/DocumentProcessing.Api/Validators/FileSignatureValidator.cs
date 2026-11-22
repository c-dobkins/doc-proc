using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Api.Validators;

/// <summary>
/// Checks an uploaded file's leading bytes ("magic numbers") against the type implied by its
/// extension, so a renamed executable or HTML file isn't accepted as a PDF or image.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly Dictionary<FileType, byte[][]> Signatures = new()
    {
        [FileType.Pdf] = [[0x25, 0x50, 0x44, 0x46, 0x2D]],                         // %PDF-
        [FileType.Jpeg] = [[0xFF, 0xD8, 0xFF]],
        [FileType.Png] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [FileType.Tiff] = [[0x49, 0x49, 0x2A, 0x00], [0x4D, 0x4D, 0x00, 0x2A]],      // little / big endian
        [FileType.Bmp] = [[0x42, 0x4D]]                                              // BM
    };

    private static readonly int MaxSignatureLength = Signatures.Values.SelectMany(s => s).Max(s => s.Length);

    public static async Task<bool> MatchesAsync(Stream stream, FileType fileType, CancellationToken ct)
    {
        if (!Signatures.TryGetValue(fileType, out var candidates))
            return false;

        var header = new byte[MaxSignatureLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        return candidates.Any(signature =>
            read >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature));
    }
}
