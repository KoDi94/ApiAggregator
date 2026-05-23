using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ApiAggregator.Domain.Interfaces;

namespace ApiAggregator.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultExpiration;

    public CacheService(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        var seconds = configuration.GetValue<int>("Cache:DefaultExpirationSeconds", 10);
        _defaultExpiration = TimeSpan.FromSeconds(seconds);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        _cache.Set(key, value, expiration);
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        _cache.Set(key, value, _defaultExpiration);
        return Task.CompletedTask;
    }
}
