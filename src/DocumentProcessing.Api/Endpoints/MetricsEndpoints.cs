using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;

namespace DocumentProcessing.Api.Endpoints;

public static class MetricsEndpoints
{
    public static RouteGroupBuilder MapMetricsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/metrics").WithTags("Metrics");

        group.MapGet("/dashboard", GetDashboardMetrics)
            .Produces<DashboardMetricsDto>();

        return group;
    }

    private static async Task<IResult> GetDashboardMetrics(
        IDocumentRepository documentRepository,
        CancellationToken ct)
    {
        var counts = await documentRepository.GetStatusCountsAsync(ct);

        counts.TryGetValue(DocumentStatus.Uploaded, out var uploaded);
        counts.TryGetValue(DocumentStatus.Processing, out var processing);
        counts.TryGetValue(DocumentStatus.Completed, out var completed);
        counts.TryGetValue(DocumentStatus.Failed, out var failed);
        counts.TryGetValue(DocumentStatus.NeedsReview, out var needsReview);
        counts.TryGetValue(DocumentStatus.Approved, out var approved);

        var total = uploaded + processing + completed + failed + needsReview + approved;

        return Results.Ok(new DashboardMetricsDto(total, uploaded, processing, completed, failed, needsReview, approved));
    }
}
