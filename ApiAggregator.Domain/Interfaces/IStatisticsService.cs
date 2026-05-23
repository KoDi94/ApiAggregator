using ApiAggregator.Domain.Models;

namespace ApiAggregator.Domain.Interfaces;

public interface IStatisticsService
{
    void TrackRequest(string apiName, long responseTimeMs);
    List<ApiStatistics> GetStatistics();
    (long OverallAvg, long Last5MinAvg) GetAveragesForApi(string apiName);
}
