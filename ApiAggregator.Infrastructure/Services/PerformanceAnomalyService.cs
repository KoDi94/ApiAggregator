using ApiAggregator.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiAggregator.Infrastructure.Services;

public class PerformanceAnomalyService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PerformanceAnomalyService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    public PerformanceAnomalyService(IServiceProvider serviceProvider, ILogger<PerformanceAnomalyService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance anomaly monitoring started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_checkInterval, stoppingToken);
            CheckForAnomalies();
        }
    }

    private void CheckForAnomalies()
    {
        using var scope = _serviceProvider.CreateScope();
        var statistics = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

        var apiClients = scope.ServiceProvider.GetServices<Domain.Interfaces.IExternalApiClient>();
        foreach (var client in apiClients)
        {
            var (overallAvg, last5MinAvg) = statistics.GetAveragesForApi(client.Name);

            if (overallAvg > 0 && last5MinAvg > 0)
            {
                var ratio = (double)last5MinAvg / overallAvg;
                if (ratio > 1.5)
                {
                    _logger.LogWarning(
                        "PERFORMANCE ANOMALY: {ApiName} - Last 5 min avg ({Last5MinAvg}ms) is {Ratio:P0} higher than overall avg ({OverallAvg}ms)",
                        client.Name, last5MinAvg, ratio - 1, overallAvg);
                }
            }
        }
    }
}
