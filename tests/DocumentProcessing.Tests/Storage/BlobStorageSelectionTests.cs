using DocumentProcessing.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.Tests.Storage;

public class BlobStorageSelectionTests
{
    private static BlobStorageSelection Resolve(Dictionary<string, string?> settings) =>
        BlobStorageSelection.Resolve(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    [Fact]
    public void Resolve_NothingConfigured_UsesLocal()
    {
        var selection = Resolve([]);

        Assert.Equal(BlobStorageProvider.Local, selection.Provider);
        Assert.Equal("documents", selection.ContainerName);
    }

    [Fact]
    public void Resolve_AzdBlobEndpoint_UsesAzureWithManagedIdentity()
    {
        var selection = Resolve(new() { ["AZURE_STORAGE_BLOB_ENDPOINT"] = "https://stdocproc.blob.core.windows.net/" });

        Assert.Equal(BlobStorageProvider.Azure, selection.Provider);
        Assert.Equal(new Uri("https://stdocproc.blob.core.windows.net/"), selection.ServiceUri);
        Assert.Null(selection.ConnectionString);
    }

    [Fact]
    public void Resolve_AspireConnectionString_UsesAzure()
    {
        var selection = Resolve(new() { ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true" });

        Assert.Equal(BlobStorageProvider.Azure, selection.Provider);
        Assert.Equal("UseDevelopmentStorage=true", selection.ConnectionString);
    }

    [Fact]
    public void Resolve_ExplicitSettingsWinOverAmbientOnes()
    {
        var selection = Resolve(new()
        {
            ["BlobStorage:Azure:ConnectionString"] = "explicit",
            ["ConnectionStrings:blobs"] = "aspire",
            ["BlobStorage:Azure:ContainerName"] = "uploads"
        });

        Assert.Equal("explicit", selection.ConnectionString);
        Assert.Equal("uploads", selection.ContainerName);
    }

    [Fact]
    public void Resolve_LocalForced_IgnoresAzureSettings()
    {
        var selection = Resolve(new()
        {
            ["BlobStorage:Provider"] = "Local",
            ["AZURE_STORAGE_BLOB_ENDPOINT"] = "https://stdocproc.blob.core.windows.net/"
        });

        Assert.Equal(BlobStorageProvider.Local, selection.Provider);
    }

    [Fact]
    public void Resolve_AzureForcedWithoutSettings_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Resolve(new() { ["BlobStorage:Provider"] = "Azure" }));
    }
}
