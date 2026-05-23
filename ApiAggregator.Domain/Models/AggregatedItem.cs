namespace ApiAggregator.Domain.Models;

public class AggregatedItem
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public required string Source { get; set; }
    public DateTime Date { get; set; }
    public required string Url { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
