using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.Infrastructure.Storage;

public enum BlobStorageProvider
{
    /// <summary>Azure Blob Storage when an Azure connection string or endpoint is configured, otherwise local.</summary>
    Auto,
    Local,
    Azure
}

/// <summary>
/// Decides which <see cref="Core.Interfaces.IBlobStorage"/> to use from configuration.
///
/// Azure settings are looked up in order of precedence:
/// <list type="bullet">
/// <item><c>BlobStorage:Azure:ConnectionString</c>, then <c>ConnectionStrings:blobs</c> (set by the Aspire
/// AppHost for the Azurite emulator). Connection strings use account keys; meant for local/dev.</item>
/// <item><c>BlobStorage:Azure:ServiceUri</c>, then <c>AZURE_STORAGE_BLOB_ENDPOINT</c> (set by azd for a
/// <c>storage</c> resource). Authenticates with managed identity / DefaultAzureCredential (keyless).</item>
/// </list>
/// </summary>
public sealed record BlobStorageSelection(
    BlobStorageProvider Provider,
    string? ConnectionString,
    Uri? ServiceUri,
    string ContainerName)
{
    public const string SectionName = "BlobStorage";
    public const string DefaultContainerName = "documents";

    public static BlobStorageSelection Resolve(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var connectionString = NullIfEmpty(section["Azure:ConnectionString"]) ?? NullIfEmpty(configuration.GetConnectionString("blobs"));
        var serviceUri = NullIfEmpty(section["Azure:ServiceUri"]) ?? NullIfEmpty(configuration["AZURE_STORAGE_BLOB_ENDPOINT"]);
        var containerName = NullIfEmpty(section["Azure:ContainerName"]) ?? DefaultContainerName;
        var requested = section.GetValue("Provider", BlobStorageProvider.Auto);

        var azureConfigured = connectionString is not null || serviceUri is not null;
        var provider = requested switch
        {
            BlobStorageProvider.Auto => azureConfigured ? BlobStorageProvider.Azure : BlobStorageProvider.Local,
            BlobStorageProvider.Azure when !azureConfigured => throw new InvalidOperationException(
                "BlobStorage:Provider is Azure but no connection string or service URI is configured " +
                "(BlobStorage:Azure:ConnectionString, ConnectionStrings:blobs, BlobStorage:Azure:ServiceUri or AZURE_STORAGE_BLOB_ENDPOINT)."),
            _ => requested
        };

        return new BlobStorageSelection(
            provider,
            connectionString,
            serviceUri is null ? null : new Uri(serviceUri),
            containerName);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
