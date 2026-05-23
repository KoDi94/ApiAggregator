namespace ApiAggregator.Domain.Models;

public class ApiStatistics
{
    public required string ApiName { get; set; }
    public int TotalRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public PerformanceBucket Buckets { get; set; } = new();
}
