using System.Diagnostics;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;

namespace ApiAggregator.Infrastructure.Clients;

public abstract class BaseApiClient(HttpClient http) : IExternalApiClient
{
    protected readonly HttpClient Http = http;

    public abstract string Name { get; }
    public abstract string Category { get; }
    protected abstract bool HasCredentials { get; }
    protected abstract Task<List<AggregatedItem>> FetchFromApiAsync(CancellationToken ct);

    public async Task<ApiClientResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!HasCredentials)
        {
            sw.Stop();
            return Error($"{Name} API key not configured", sw.ElapsedMilliseconds);
        }

        try
        {
            var items = await FetchFromApiAsync(cancellationToken);
            sw.Stop();
            return Ok(items, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Error($"{Name} error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    private ApiClientResult Ok(List<AggregatedItem> data, long ms)
    {
        return new ApiClientResult { Success = true, Source = Name, Data = data, ResponseTimeMs = ms };
    }

    private ApiClientResult Error(string message, long ms)
    {
        return new ApiClientResult { Success = false, Source = Name, ErrorMessage = message, ResponseTimeMs = ms };
    }
}
