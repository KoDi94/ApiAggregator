using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;

namespace ApiAggregator.Infrastructure.Services;

public class StatisticsService : IStatisticsService
{
    private readonly Dictionary<string, List<long>> _responseTimes = new();
    private readonly Dictionary<string, List<DateTime>> _timestamps = new();
    private readonly ReaderWriterLockSlim _rwLock = new();

    public void TrackRequest(string apiName, long responseTimeMs)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (!_responseTimes.TryGetValue(apiName, out var times))
            {
                times = [];
                _responseTimes[apiName] = times;
            }
            times.Add(responseTimeMs);

            if (!_timestamps.TryGetValue(apiName, out var timestamps))
            {
                timestamps = [];
                _timestamps[apiName] = timestamps;
            }
            timestamps.Add(DateTime.UtcNow);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public List<ApiStatistics> GetStatistics()
    {
        _rwLock.EnterReadLock();
        try
        {
            var stats = new List<ApiStatistics>();
            foreach (var kvp in _responseTimes)
            {
                var times = kvp.Value;
                var avg = times.Count > 0 ? times.Average() : 0;
                stats.Add(new ApiStatistics
                {
                    ApiName = kvp.Key,
                    TotalRequests = times.Count,
                    AverageResponseTimeMs = Math.Round(avg, 1),
                    Buckets = CalculateBuckets(times)
                });
            }
            return stats;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public (long OverallAvg, long Last5MinAvg) GetAveragesForApi(string apiName)
    {
        _rwLock.EnterReadLock();
        try
        {
            if (!_responseTimes.TryGetValue(apiName, out var times) || times.Count == 0)
                return (0, 0);

            var overallAvg = (long)times.Average();

            if (!_timestamps.TryGetValue(apiName, out var timestamps))
                return (overallAvg, 0);

            var fiveMinAgo = DateTime.UtcNow.AddMinutes(-5);
            var recentIndices = timestamps
                .Select((t, i) => (t, i))
                .Where(x => x.t >= fiveMinAgo)
                .Select(x => x.i)
                .ToArray();

            if (recentIndices.Length == 0)
                return (overallAvg, 0);

            var recentAvg = (long)recentIndices.Select(i => times[i]).Average();
            return (overallAvg, recentAvg);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private static PerformanceBucket CalculateBuckets(List<long> times)
    {
        var bucket = new PerformanceBucket();
        foreach (var t in times)
        {
            if (t < 100) bucket.Fast++;
            else if (t <= 250) bucket.Average++;
            else bucket.Slow++;
        }
        return bucket;
    }
}
