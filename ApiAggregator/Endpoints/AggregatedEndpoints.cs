using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;

namespace ApiAggregator.Endpoints;

public static class AggregatedEndpoints
{
    public static void MapAggregatedEndpoints(this WebApplication app)
    {
        app.MapGet("/api/aggregated", async (
            HttpContext httpContext,
            [AsParameters] AggregationQuery query,
            IAggregationService aggregationService,
            CancellationToken cancellationToken) =>
        {
            var result = await aggregationService.GetAggregatedDataAsync(query, cancellationToken);
            httpContext.Response.Headers["X-Cache"] = result.CacheSources.Count > 0
                ? string.Join(", ", result.CacheSources)
                : "MISS";
            return Results.Ok(result);
        })
        .WithName("GetAggregatedData")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
