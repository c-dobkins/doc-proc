namespace DocumentProcessing.Infrastructure.Storage;

/// <summary>
/// Storage path conventions shared by every <see cref="Core.Interfaces.IBlobStorage"/> implementation,
/// so a document saved by one backend has the same path shape as in any other.
/// </summary>
internal static class StoragePaths
{
    /// <summary>
    /// Builds a new relative path <c>yyyy/MM/dd/{guid}_{fileName}</c>. Any client-supplied directory is
    /// stripped (whichever separator style it uses) and '/' is always the separator.
    /// </summary>
    public static string NewRelativePath(string fileName)
    {
        var sanitizedFileName = Path.GetFileName(fileName.Replace('\\', '/'));
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}_{sanitizedFileName}";
    }

    /// <summary>Normalizes separators to '/'.</summary>
    public static string Normalize(string storagePath) => storagePath.Replace('\\', '/');
}
