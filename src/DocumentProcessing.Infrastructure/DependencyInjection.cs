using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ai;
using DocumentProcessing.Infrastructure.Data;
using DocumentProcessing.Infrastructure.Data.Repositories;
using DocumentProcessing.Infrastructure.Notifications;
using DocumentProcessing.Infrastructure.Ocr;
using DocumentProcessing.Infrastructure.Pipeline;
using DocumentProcessing.Infrastructure.Storage;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<DocumentProcessingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("documentprocessing"),
                npgsql => npgsql.MigrationsAssembly(typeof(DocumentProcessingDbContext).Assembly.FullName)));

        // Repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IResultStore, ResultStore>();
        services.AddScoped<ICorrectionLogRepository, CorrectionLogRepository>();

        // Blob storage: Azure Blob when configured (production / Aspire's Azurite), else local disk
        var blobStorage = BlobStorageSelection.Resolve(configuration);
        services.AddSingleton(blobStorage);
        if (blobStorage.Provider == BlobStorageProvider.Azure)
        {
            services.AddSingleton(_ =>
            {
                var serviceClient = blobStorage.ConnectionString is not null
                    ? new BlobServiceClient(blobStorage.ConnectionString)
                    : new BlobServiceClient(blobStorage.ServiceUri, new DefaultAzureCredential());
                return serviceClient.GetBlobContainerClient(blobStorage.ContainerName);
            });
            services.AddSingleton<IBlobStorage, AzureBlobStorage>();
        }
        else
        {
            services.Configure<LocalBlobStorageOptions>(configuration.GetSection(BlobStorageSelection.SectionName));
            services.AddSingleton<IBlobStorage, LocalBlobStorage>();
        }

        // OCR
        services.Configure<TesseractOptions>(configuration.GetSection("Tesseract"));
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<ITesseractEngine, TesseractOcrEngine>();
        services.AddSingleton<IOcrEngine, OcrEngineRouter>();

        // Pre-processing
        services.AddSingleton<IPreProcessor, DocumentPreProcessor>();

        // Post-processing
        services.AddScoped<IPostProcessor, PostProcessor>();

        // AI providers
        services.Configure<AiProviderOptions>(configuration.GetSection(AiProviderOptions.SectionName));

        var aiConfig = configuration.GetSection(AiProviderOptions.SectionName).Get<AiProviderOptions>();

        if (aiConfig?.AzureOpenAi.Enabled == true)
            services.AddSingleton<IAiExtractionProvider, AzureOpenAiProvider>();

        if (aiConfig?.OpenAi.Enabled == true)
            services.AddSingleton<IAiExtractionProvider, OpenAiProvider>();

        if (aiConfig?.Gemini.Enabled == true)
        {
            services.AddHttpClient("Gemini");
            services.AddSingleton<IAiExtractionProvider, GoogleGeminiProvider>();
        }

        if (aiConfig?.Ollama.Enabled == true)
        {
            services.AddHttpClient("Ollama");
            services.AddSingleton<IAiExtractionProvider, OllamaProvider>();
        }

        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();

        // Pipeline
        services.Configure<ProcessingOptions>(configuration.GetSection(ProcessingOptions.SectionName));
        services.Configure<PostProcessorOptions>(configuration.GetSection(PostProcessorOptions.SectionName));
        services.AddScoped<IIngestionHandler, IngestionHandler>();
        services.AddScoped<IDocumentPipeline, DocumentPipeline>();

        // Notifications — default no-op; API overrides with SignalRProcessingNotifier
        services.AddScoped<IProcessingNotifier, NullProcessingNotifier>();

        return services;
    }
}
