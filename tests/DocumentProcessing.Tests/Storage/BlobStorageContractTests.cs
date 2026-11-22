using Azure.Storage.Blobs;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Tests.Storage;

/// <summary>
/// Behaviour every <see cref="IBlobStorage"/> must share, so documents behave the same whichever
/// backend a deployment uses. Runs against local disk always, and Azure Blob Storage (Azurite) when
/// DOCPROC_TEST_AZURITE holds a connection string (CI sets it).
/// </summary>
public abstract class BlobStorageContractTests
{
    protected abstract IBlobStorage Storage { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsContent()
    {
        var content = "%PDF-1.7 round trip"u8.ToArray();

        var path = await Storage.SaveAsync(new MemoryStream(content), "invoice.pdf", Ct);

        await using var stream = await Storage.GetAsync(path, Ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Ct);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task SaveAsync_ReturnsDatedRelativePathWithForwardSlashes()
    {
        var path = await Storage.SaveAsync(new MemoryStream([1]), @"C:\Users\someone\scan.png", Ct);

        Assert.Matches(@"^\d{4}/\d{2}/\d{2}/[0-9a-f]{32}_scan\.png$", path);
    }

    [Fact]
    public async Task SaveAsync_SameFileNameTwice_StoresSeparately()
    {
        var first = await Storage.SaveAsync(new MemoryStream([1]), "same.pdf", Ct);
        var second = await Storage.SaveAsync(new MemoryStream([2]), "same.pdf", Ct);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task GetAsync_MissingFile_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => Storage.GetAsync("2026/01/01/missing.pdf", Ct));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile_AndIsIdempotent()
    {
        var path = await Storage.SaveAsync(new MemoryStream([1, 2, 3]), "delete-me.pdf", Ct);

        await Storage.DeleteAsync(path, Ct);
        await Storage.DeleteAsync(path, Ct);

        await Assert.ThrowsAsync<FileNotFoundException>(() => Storage.GetAsync(path, Ct));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData(@"..\..\outside.txt")]
    public async Task GetAsync_TraversalPath_ThrowsUnauthorizedAccessException(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Storage.GetAsync(path, Ct));
    }
}

public sealed class LocalBlobStorageContractTests : BlobStorageContractTests, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "docproc-blob-" + Guid.NewGuid().ToString("N"));

    public LocalBlobStorageContractTests() =>
        Storage = new LocalBlobStorage(Options.Create(new LocalBlobStorageOptions { BasePath = _dir }));

    protected override IBlobStorage Storage { get; }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

public sealed class AzureBlobStorageContractTests : BlobStorageContractTests, IAsyncLifetime
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("DOCPROC_TEST_AZURITE");
    private readonly BlobContainerClient? _container;

    public AzureBlobStorageContractTests()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString),
            "Set DOCPROC_TEST_AZURITE to an Azurite/Azure Storage connection string to run");

        // A fresh container per test; AzureBlobStorage creates it on first save
        _container = new BlobServiceClient(ConnectionString).GetBlobContainerClient($"test-{Guid.NewGuid():N}");
        Storage = new AzureBlobStorage(_container, NullLogger<AzureBlobStorage>.Instance);
    }

    protected override IBlobStorage Storage { get; } = null!;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DeleteIfExistsAsync();
    }
}
