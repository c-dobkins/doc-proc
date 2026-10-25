using DocumentProcessing.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Tests;

public class LocalBlobStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalBlobStorage _sut;

    public LocalBlobStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new LocalBlobStorageOptions { BasePath = _tempDir });
        _sut = new LocalBlobStorage(options);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task GetAsync_ValidPath_ReturnsFileContent()
    {
        var content = "hello test"u8.ToArray();
        var relativePath = "2024/01/01/testfile.txt";
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, TestContext.Current.CancellationToken);

        await using var stream = await _sut.GetAsync(relativePath, CancellationToken.None);

        var buffer = new byte[content.Length];
        _ = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(content, buffer);
    }

    [Fact]
    public async Task GetAsync_PathTraversalAttempt_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetAsync("../../etc/passwd", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_EncodedPathTraversal_ThrowsUnauthorizedAccessException()
    {
        // Path.GetFullPath resolves ..\.. sequences; this verifies the guard catches it
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetAsync(@"..\..\..\sensitive.txt", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_SiblingDirectorySharingPrefix_ThrowsUnauthorizedAccessException()
    {
        // e.g. base "/tmp/abc" must not grant access to "/tmp/abc-other/secret.txt"
        var sibling = Path.GetFileName(_tempDir) + "-other";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetAsync($"../{sibling}/secret.txt", CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_FileNameWithClientDirectory_StoresOnlyFileNameAndRoundTrips()
    {
        var content = "round trip"u8.ToArray();

        var storagePath = await _sut.SaveAsync(new MemoryStream(content), @"C:\Users\someone\invoice.pdf", CancellationToken.None);

        Assert.DoesNotContain('\\', storagePath);
        Assert.EndsWith("_invoice.pdf", storagePath);

        await using var stream = await _sut.GetAsync(storagePath, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task GetAsync_MissingFile_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.GetAsync("does/not/exist.pdf", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_PathTraversalAttempt_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAsync("../../sensitive.txt", CancellationToken.None));
    }
}
