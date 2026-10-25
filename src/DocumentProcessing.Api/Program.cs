using DocumentProcessing.Api;
using DocumentProcessing.Api.Auth;
using DocumentProcessing.Api.Endpoints;
using DocumentProcessing.Api.Hubs;
using DocumentProcessing.Api.Middleware;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure;
using DocumentProcessing.Worker;
using FluentValidation;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery, resilience)
    builder.AddServiceDefaults();

    // Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Infrastructure services (EF Core, repositories, blob storage, pipeline)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Channel for async document processing (ADR-0004)
    var processingChannel = new DocumentProcessingChannel();
    builder.Services.AddSingleton(processingChannel);
    builder.Services.AddSingleton(processingChannel.Reader);
    builder.Services.AddSingleton(processingChannel.Writer);

    // Background worker for document processing
    builder.Services.AddHostedService<DocumentProcessingWorker>();

    // SignalR
    builder.Services.AddSignalR();
    builder.Services.AddScoped<IProcessingNotifier, SignalRProcessingNotifier>();

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddTransient<GlobalExceptionHandler>();

    // API-key protection for write/processing endpoints (reads stay public); see SECURITY.md
    builder.Services.AddApiKeyAuthentication(builder.Configuration);

    // Rate limit endpoints that trigger OCR/AI processing. The API has no authentication, so this
    // caps how fast a public deployment can burn AI quota (shared window across all callers).
    var processingPermitsPerMinute = builder.Configuration.GetValue("RateLimiting:ProcessingPermitsPerMinute", 30);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter(DocumentEndpoints.ProcessingRateLimitPolicy, limiter =>
        {
            limiter.PermitLimit = processingPermitsPerMinute;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
    });

    // OpenAPI
    builder.Services.AddOpenApi();

    // CORS for React frontend
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    var app = builder.Build();

    var storageSelection = app.Services.GetRequiredService<DocumentProcessing.Infrastructure.Storage.BlobStorageSelection>();
    if (storageSelection.Provider == DocumentProcessing.Infrastructure.Storage.BlobStorageProvider.Local && !app.Environment.IsDevelopment())
        Log.Warning("Uploads are stored on the local filesystem; on Azure Container Apps they are lost on restart. Configure Azure Blob Storage (see README)");
    else
        Log.Information("Blob storage: {Provider} (container {Container})", storageSelection.Provider, storageSelection.ContainerName);

    var authOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiKeyOptions>>().Value;
    if (authOptions.RequireApiKeyForWrites && authOptions.ApiKeys.Count == 0)
        Log.Warning("Auth:RequireApiKeyForWrites is on but no Auth:ApiKeys are configured: the app is read-only");

    // Apply database migrations (baselining databases created before migrations existed), then seed
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DocumentProcessing.Infrastructure.Data.DocumentProcessingDbContext>();
        var dbLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        await DocumentProcessing.Infrastructure.Data.DatabaseInitializer.InitializeAsync(db, dbLogger);

        // Seed sample templates and documents on first run
        var blobStorage = scope.ServiceProvider.GetRequiredService<DocumentProcessing.Core.Interfaces.IBlobStorage>();
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        await DocumentProcessing.Infrastructure.Data.SeedData.SeedAsync(db, blobStorage, seedLogger);
    }

    // Middleware pipeline
    app.UseMiddleware<GlobalExceptionHandler>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    // Authorization runs before the rate limiter so rejected (unauthenticated) calls don't use up permits
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();

        // Serve React SPA from wwwroot in production
        app.UseDefaultFiles();
        app.UseStaticFiles();
    }

    // Map endpoints
    app.MapDocumentEndpoints();
    app.MapTemplateEndpoints();
    app.MapMetricsEndpoints();
    app.MapAuthEndpoints();

    // SignalR hub
    app.MapHub<ProcessingHub>("/hubs/processing");

    // Aspire's MapDefaultEndpoints maps /health and /alive in Development only (mapping /health
    // here as well made the route ambiguous and return 500). Expose /health elsewhere for probes.
    if (!app.Environment.IsDevelopment())
    {
        app.MapHealthChecks("/health");
    }

    app.MapDefaultEndpoints();

    // SPA fallback — serve index.html for client-side routes in production
    if (!app.Environment.IsDevelopment())
    {
        app.MapFallbackToFile("index.html");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
