namespace ApiAggregator.Domain.Models;

public class ApiStatistics
{
    public string ApiName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public PerformanceBucket Buckets { get; set; } = new();
}
