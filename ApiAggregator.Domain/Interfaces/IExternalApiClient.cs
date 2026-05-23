using ApiAggregator.Domain.Models;

namespace ApiAggregator.Domain.Interfaces;

public interface IExternalApiClient
{
    string Name { get; }
    string Category { get; }
    Task<ApiClientResult> FetchAsync(CancellationToken cancellationToken = default);
}
