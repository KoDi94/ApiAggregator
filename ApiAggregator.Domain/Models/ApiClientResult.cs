namespace ApiAggregator.Domain.Models;

public class ApiClientResult
{
    public bool Success { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<AggregatedItem> Data { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool FromCache { get; set; }
}
