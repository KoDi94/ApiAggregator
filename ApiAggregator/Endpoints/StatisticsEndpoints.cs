using ApiAggregator.Domain.Interfaces;

namespace ApiAggregator.Endpoints;

public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/statistics", (IStatisticsService statisticsService) =>
        {
            var stats = statisticsService.GetStatistics();
            return Results.Ok(stats);
        })
        .WithName("GetStatistics")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
