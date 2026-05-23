using ApiAggregator.Application.Services;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiAggregator.Tests.Services;

public class AggregationServiceTests
{
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IStatisticsService> _statsMock;
    private readonly Mock<ILogger<AggregationService>> _loggerMock;
    private readonly List<IExternalApiClient> _clients;

    private static readonly List<AggregatedItem> WeatherData =
    [
        new() { Id = "w1", Title = "Sunny London", Description = "sunny", Category = "Weather", Source = "WeatherSim", Date = new DateTime(2024, 6, 1), Url = "https://example.com/w1" },
        new() { Id = "w2", Title = "Rainy Paris", Description = "rainy", Category = "Weather", Source = "WeatherSim", Date = new DateTime(2024, 6, 3), Url = "https://example.com/w2" }
    ];

    private static readonly List<AggregatedItem> NewsData =
    [
        new() { Id = "n1", Title = "Quantum Breakthrough", Description = "quantum computing advances", Category = "Tech", Source = "NewsSim", Date = new DateTime(2024, 6, 2), Url = "https://example.com/n1" },
        new() { Id = "n2", Title = "Sports Final", Description = "sports news", Category = "Sports", Source = "NewsSim", Date = new DateTime(2024, 5, 30), Url = "https://example.com/n2" }
    ];

    private static readonly List<AggregatedItem> MusicData =
    [
        new() { Id = "m1", Title = "New Album Drop", Description = "new music", Category = "Music", Source = "MusicSim", Date = new DateTime(2024, 6, 5), Url = "https://example.com/m1" }
    ];

    public AggregationServiceTests()
    {
        _cacheMock = new Mock<ICacheService>();
        _statsMock = new Mock<IStatisticsService>();
        _loggerMock = new Mock<ILogger<AggregationService>>();

        _cacheMock.Setup(x => x.GetAsync<List<AggregatedItem>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AggregatedItem>?)null);

        var weatherMock = new Mock<IExternalApiClient>();
        weatherMock.Setup(x => x.Name).Returns("WeatherSim");
        weatherMock.Setup(x => x.Category).Returns("Weather");
        weatherMock.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiClientResult { Success = true, Source = "WeatherSim", Data = WeatherData });

        var newsMock = new Mock<IExternalApiClient>();
        newsMock.Setup(x => x.Name).Returns("NewsSim");
        newsMock.Setup(x => x.Category).Returns("News");
        newsMock.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiClientResult { Success = true, Source = "NewsSim", Data = NewsData });

        var musicMock = new Mock<IExternalApiClient>();
        musicMock.Setup(x => x.Name).Returns("MusicSim");
        musicMock.Setup(x => x.Category).Returns("Music");
        musicMock.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiClientResult { Success = true, Source = "MusicSim", Data = MusicData });

        _clients = [weatherMock.Object, newsMock.Object, musicMock.Object];
    }

    [Fact]
    public async Task GetAggregatedDataAsync_ReturnsDataFromAllClients()
    {
        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery { PageSize = 100 });

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(3, result.Sources.Count);
        Assert.Contains("WeatherSim", result.Sources);
        Assert.Contains("NewsSim", result.Sources);
        Assert.Contains("MusicSim", result.Sources);
    }

    [Fact]
    public async Task GetAggregatedDataAsync_FiltersByCategory()
    {
        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery
        {
            Category = "Weather",
            PageSize = 100
        });

        Assert.All(result.Items, item => Assert.Equal("Weather", item.Category));
    }

    [Fact]
    public async Task GetAggregatedDataAsync_FiltersBySearch()
    {
        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery
        {
            Search = "quantum",
            PageSize = 100
        });

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetAggregatedDataAsync_PaginatesResults()
    {
        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery
        {
            Page = 1,
            PageSize = 2
        });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetAggregatedDataAsync_SortsByDateDescending()
    {
        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery
        {
            SortBy = "date",
            SortOrder = "desc",
            PageSize = 100
        });

        for (int i = 1; i < result.Items.Count; i++)
        {
            Assert.True(result.Items[i - 1].Date >= result.Items[i].Date);
        }
    }

    [Fact]
    public async Task GetAggregatedDataAsync_UsesCache()
    {
        var cachedData = new List<AggregatedItem>
        {
            new() { Id = "cached-1", Title = "Cached Item", Description = "cached", Category = "Weather", Source = "WeatherSim", Url = "https://example.com/cached" }
        };

        _cacheMock.Setup(x => x.GetAsync<List<AggregatedItem>>("api_data_WeatherSim", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);
        _cacheMock.Setup(x => x.GetAsync<List<AggregatedItem>>("api_data_NewsSim", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AggregatedItem>?)null);
        _cacheMock.Setup(x => x.GetAsync<List<AggregatedItem>>("api_data_MusicSim", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AggregatedItem>?)null);

        var service = CreateService();
        var result = await service.GetAggregatedDataAsync(new AggregationQuery { PageSize = 100 });

        Assert.Contains(result.Items, i => i.Id == "cached-1");
    }

    [Fact]
    public async Task GetAggregatedDataAsync_TracksStatistics()
    {
        var service = CreateService();
        await service.GetAggregatedDataAsync(new AggregationQuery { PageSize = 100 });

        _statsMock.Verify(x => x.TrackRequest(It.IsAny<string>(), It.IsAny<long>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task GetAggregatedDataAsync_ReturnsPartialResults_OnFailure()
    {
        var failingMock = new Mock<IExternalApiClient>();
        failingMock.Setup(x => x.Name).Returns("Failing");
        failingMock.Setup(x => x.Category).Returns("Failing");
        failingMock.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiClientResult { Success = false, Source = "Failing", ErrorMessage = "Down" });

        var allClients = new List<IExternalApiClient>(_clients) { failingMock.Object };
        var service = new AggregationService(allClients, _cacheMock.Object, _statsMock.Object, _loggerMock.Object);

        var result = await service.GetAggregatedDataAsync(new AggregationQuery { PageSize = 100 });

        Assert.Equal(5, result.Items.Count);
        Assert.Contains(result.Errors, e => e.Contains("Down"));
    }

    private AggregationService CreateService()
    {
        return new AggregationService(_clients, _cacheMock.Object, _statsMock.Object, _loggerMock.Object);
    }
}
