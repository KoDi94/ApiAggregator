using ApiAggregator.Domain.Models;

namespace ApiAggregator.Domain.Interfaces;

public interface IAggregationService
{
    Task<AggregatedResponse> GetAggregatedDataAsync(AggregationQuery query, CancellationToken cancellationToken = default);
}
