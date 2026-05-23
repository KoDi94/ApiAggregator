namespace ApiAggregator.Domain.Models;

public class ApiClientResult
{
    public bool Success { get; set; }
    public required string Source { get; set; }
    public List<AggregatedItem> Data { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool FromCache { get; set; }
}
