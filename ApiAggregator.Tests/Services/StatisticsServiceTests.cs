using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Services;

namespace ApiAggregator.Tests.Services;

public class StatisticsServiceTests
{
    [Fact]
    public void GetStatistics_ReturnsEmpty_WhenNoRequests()
    {
        IStatisticsService service = new StatisticsService();
        var stats = service.GetStatistics();
        Assert.Empty(stats);
    }

    [Fact]
    public void TrackRequest_IncrementsTotalRequests()
    {
        IStatisticsService service = new StatisticsService();
        service.TrackRequest("Weather", 50);
        service.TrackRequest("Weather", 150);

        var stats = service.GetStatistics();
        var weatherStats = stats.Single(s => s.ApiName == "Weather");

        Assert.Equal(2, weatherStats.TotalRequests);
        Assert.Equal(100, weatherStats.AverageResponseTimeMs);
    }

    [Fact]
    public void TrackRequest_AssignsCorrectBuckets()
    {
        IStatisticsService service = new StatisticsService();
        service.TrackRequest("News", 50);
        service.TrackRequest("News", 150);
        service.TrackRequest("News", 300);

        var stats = service.GetStatistics();
        var newsStats = stats.Single(s => s.ApiName == "News");

        Assert.Equal(1, newsStats.Buckets.Fast);
        Assert.Equal(1, newsStats.Buckets.Average);
        Assert.Equal(1, newsStats.Buckets.Slow);
    }

    [Fact]
    public void TrackRequest_HandlesMultipleApis()
    {
        IStatisticsService service = new StatisticsService();
        service.TrackRequest("Weather", 100);
        service.TrackRequest("News", 200);
        service.TrackRequest("GitHub", 300);

        var stats = service.GetStatistics();
        Assert.Equal(3, stats.Count);
    }

    [Theory]
    [InlineData(50, "Fast")]
    [InlineData(150, "Average")]
    [InlineData(300, "Slow")]
    public void TrackRequest_ClassifiesResponseTimeCorrectly(long responseTimeMs, string expectedBucket)
    {
        IStatisticsService service = new StatisticsService();
        service.TrackRequest("Test", responseTimeMs);

        var stats = service.GetStatistics();
        var testStats = stats.Single(s => s.ApiName == "Test");

        Assert.Equal(1, testStats.TotalRequests);
        Assert.Equal(responseTimeMs, testStats.AverageResponseTimeMs);

        switch (expectedBucket)
        {
            case "Fast":
                Assert.Equal(1, testStats.Buckets.Fast);
                break;
            case "Average":
                Assert.Equal(1, testStats.Buckets.Average);
                break;
            case "Slow":
                Assert.Equal(1, testStats.Buckets.Slow);
                break;
        }
    }

    [Fact]
    public void GetAveragesForApi_ReturnsZero_WhenNoData()
    {
        IStatisticsService service = new StatisticsService();
        var (overallAvg, last5MinAvg) = service.GetAveragesForApi("NonExistent");
        Assert.Equal(0, overallAvg);
        Assert.Equal(0, last5MinAvg);
    }

    [Fact]
    public void GetAveragesForApi_ReturnsOverallAverage()
    {
        IStatisticsService service = new StatisticsService();
        service.TrackRequest("Weather", 100);
        service.TrackRequest("Weather", 200);

        var (overallAvg, _) = service.GetAveragesForApi("Weather");
        Assert.Equal(150, overallAvg);
    }
}
