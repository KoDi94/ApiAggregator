using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ApiAggregator.Application.Services;

public class AggregationService : IAggregationService
{
    private readonly IEnumerable<IExternalApiClient> _apiClients;
    private readonly ICacheService _cache;
    private readonly IStatisticsService _statistics;
    private readonly ILogger<AggregationService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public AggregationService(
        IEnumerable<IExternalApiClient> apiClients,
        ICacheService cache,
        IStatisticsService statistics,
        ILogger<AggregationService> logger)
    {
        _apiClients = apiClients;
        _cache = cache;
        _statistics = statistics;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry {Attempt} after error: {Error}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    public async Task<AggregatedResponse> GetAggregatedDataAsync(AggregationQuery query, CancellationToken cancellationToken = default)
    {
        var clients = _apiClients;
        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            clients = _apiClients.Where(c =>
                c.Name.Equals(query.Source, StringComparison.OrdinalIgnoreCase));
        }

        var tasks = clients.Select(client => FetchClientDataAsync(client, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var allItems = new List<AggregatedItem>();
        var errors = new List<string>();
        var cacheSources = new List<string>();

        foreach (var result in results)
        {
            if (result.Success)
            {
                allItems.AddRange(result.Data);
                if (result.FromCache)
                    cacheSources.Add(result.Source);
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
                errors.Add(result.ErrorMessage);
        }

        var filtered = ApplyFilters(allItems, query);
        var sorted = ApplySorting(filtered, query);
        var totalCount = sorted.Count;

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? 20;

        var paged = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AggregatedResponse
        {
            Items = paged,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Sources = _apiClients.Select(c => c.Name).ToList(),
            Errors = errors,
            CacheSources = cacheSources
        };
    }

    private async Task<ApiClientResult> FetchClientDataAsync(IExternalApiClient client, CancellationToken cancellationToken)
    {
        var cacheKey = $"api_data_{client.Name}";
        var cached = await _cache.GetAsync<List<AggregatedItem>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiClientResult { Success = true, Source = client.Name, Data = cached, FromCache = true };
        }

        try
        {
            var result = await _pipeline.ExecuteAsync(
                async ct => await client.FetchAsync(ct), cancellationToken);

            _statistics.TrackRequest(client.Name, result.ResponseTimeMs);

            if (result.Success && result.Data.Count > 0)
            {
                await _cache.SetAsync(cacheKey, result.Data, TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from {ApiName}", client.Name);
            _statistics.TrackRequest(client.Name, -1);
            return new ApiClientResult
            {
                Success = false,
                Source = client.Name,
                ErrorMessage = $"{client.Name} API unavailable: {ex.Message}",
                Data = []
            };
        }
    }

    private static List<AggregatedItem> ApplyFilters(List<AggregatedItem> items, AggregationQuery query)
    {
        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Category))
            filtered = filtered.Where(i => i.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Source))
            filtered = filtered.Where(i => i.Source.Equals(query.Source, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Id))
            filtered = filtered.Where(i => i.Id.Equals(query.Id, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromDate.HasValue)
            filtered = filtered.Where(i => i.Date >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            filtered = filtered.Where(i => i.Date <= query.ToDate.Value);

        return filtered.ToList();
    }

    private static List<AggregatedItem> ApplySorting(List<AggregatedItem> items, AggregationQuery query)
    {
        var descending = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "title" => descending
                ? items.OrderByDescending(i => i.Title)
                : items.OrderBy(i => i.Title),
            "category" => descending
                ? items.OrderByDescending(i => i.Category)
                : items.OrderBy(i => i.Category),
            _ => descending
                ? items.OrderByDescending(i => i.Date)
                : items.OrderBy(i => i.Date)
        };

        return sorted.ToList();
    }
}
