namespace ApiAggregator.Domain.Models;

public class AggregatedResponse
{
    public List<AggregatedItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<string> Errors { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> CacheSources { get; set; } = [];
}
