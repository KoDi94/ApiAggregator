using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Tests.Services;

public class CacheServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    public CacheServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:DefaultExpirationSeconds"] = "10"
            })
            .Build();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyNotFound()
    {
        var service = CreateService();
        var result = await service.GetAsync<string>("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetAsync_ReturnsValue()
    {
        var service = CreateService();
        await service.SetAsync("test-key", "test-value", TimeSpan.FromMinutes(5));
        var result = await service.GetAsync<string>("test-key");
        Assert.Equal("test-value", result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_AfterExpiration()
    {
        var service = CreateService();
        await service.SetAsync("expire-key", "value", TimeSpan.FromMilliseconds(1));
        await Task.Delay(10);
        var result = await service.GetAsync<string>("expire-key");
        Assert.Null(result);
    }

    private ICacheService CreateService()
    {
        return new CacheService(_memoryCache, _configuration);
    }
}
