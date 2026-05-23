using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Infrastructure.Clients;

public class NewsApiClient : IExternalApiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _country;

    public NewsApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var section = config.GetSection("ExternalApis:NewsApi");
        _apiKey = section["ApiKey"] ?? "";
        _country = section["Country"] ?? "us";
    }

    public string Name => "NewsApi";
    public string Category => "News";

    public async Task<ApiClientResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(100, cancellationToken);

        if (string.IsNullOrEmpty(_apiKey))
        {
            sw.Stop();
            return new ApiClientResult
            {
                Success = true, Source = Name, Data = GetFallbackData(),
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var items = new List<AggregatedItem>();
            var categories = new[] { "business", "technology", "science", "health", "sports", "entertainment" };
            foreach (var category in categories)
            {
                var url = $"top-headlines?country={_country}&category={category}&pageSize=1&apiKey={_apiKey}";
                var response = await _http.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<NewsResponse>(cancellationToken);
                if (data?.Articles != null)
                {
                    foreach (var article in data.Articles)
                    {
                        items.Add(new AggregatedItem
                        {
                            Id = $"news-{Guid.NewGuid():N}",
                            Title = article.Title ?? "(no title)",
                            Description = article.Description ?? "(no description)",
                            Category = category,
                            Source = Name,
                            Date = DateTime.TryParse(article.PublishedAt, out var d) ? d : DateTime.UtcNow,
                            Url = article.Url ?? "",
                            Metadata = new Dictionary<string, object>
                            {
                                ["Source"] = article.Source?.Name ?? "unknown",
                                ["Category"] = category
                            }
                        });
                    }
                }
            }

            sw.Stop();
            return new ApiClientResult
            {
                Success = true, Source = Name, Data = items, ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ApiClientResult
            {
                Success = true, Source = Name, Data = GetFallbackData(),
                ErrorMessage = $"NewsAPI error: {ex.Message}",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private static List<AggregatedItem> GetFallbackData()
    {
        var headlines = new[]
        {
            ("Tech Giant Announces New AI Platform", "technology"),
            ("Markets Reach All-Time High", "business"),
            ("Breakthrough in Renewable Energy", "science"),
            ("New Study Links Diet to Longevity", "health"),
            ("Championship Finals Set Record Viewership", "sports"),
            ("Award-Winning Film Breaks Box Office Records", "entertainment")
        };
        return headlines.Select((h, i) => new AggregatedItem
        {
            Id = $"news-{Guid.NewGuid():N}",
            Title = h.Item1,
            Description = $"In recent developments, {h.Item1.ToLowerInvariant()}...",
            Category = h.Item2,
            Source = "NewsApi",
            Date = DateTime.UtcNow.AddHours(-i * 4),
            Url = "https://newsapi.org",
            Metadata = new Dictionary<string, object>
            {
                ["Source"] = "fallback",
                ["Category"] = h.Item2
            }
        }).ToList();
    }

    private class NewsResponse
    {
        [JsonPropertyName("articles")] public Article[]? Articles { get; set; }
    }

    private class Article
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("publishedAt")] public string? PublishedAt { get; set; }
        [JsonPropertyName("source")] public SourceInfo? Source { get; set; }
    }

    private class SourceInfo
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
