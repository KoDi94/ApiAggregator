namespace ApiAggregator.Domain.Models;

public class AggregatedResponse
{
    public List<AggregatedItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<string> Sources { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> CacheSources { get; set; } = new();
}
