namespace ApiAggregator.Domain.Models;

public class AggregationQuery
{
    public string? Category { get; set; }
    public string? Source { get; set; }
    public string? Id { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
