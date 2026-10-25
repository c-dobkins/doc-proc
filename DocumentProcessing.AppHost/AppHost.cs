var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("documentprocessing");

// Azure Blob Storage emulator (Azurite), so local runs use the same storage code path as production
var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite.WithDataVolume())
    .AddBlobs("blobs");

var api = builder.AddProject<Projects.DocumentProcessing_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithExternalHttpEndpoints();

builder.AddViteApp("web", "../src/DocumentProcessing.Web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
