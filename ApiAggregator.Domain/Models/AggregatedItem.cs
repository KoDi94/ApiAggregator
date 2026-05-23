namespace ApiAggregator.Domain.Models;

public class AggregatedItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}
