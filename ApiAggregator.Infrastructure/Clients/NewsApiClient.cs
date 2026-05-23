using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Infrastructure.Clients;

public class NewsApiClient : BaseApiClient
{
    private readonly string _apiKey;
    private readonly string _country;

    public NewsApiClient(HttpClient http, IConfiguration config) : base(http)
    {
        var section = config.GetSection("ExternalApis:NewsApi");
        _apiKey = section["ApiKey"] ?? "";
        _country = section["Country"] ?? "us";
    }

    public override string Name => "NewsApi";
    public override string Category => "News";
    protected override bool HasCredentials => !string.IsNullOrEmpty(_apiKey);

    protected override async Task<List<AggregatedItem>> FetchFromApiAsync(CancellationToken ct)
    {
        var items = new List<AggregatedItem>();
        var categories = new[] { "business", "technology", "science", "health", "sports", "entertainment" };
        foreach (var category in categories)
        {
            var url = $"top-headlines?country={_country}&category={category}&pageSize=1&apiKey={_apiKey}";
            var response = await Http.GetAsync(url, ct);
            await EnsureSuccessOrThrowAsync(response, ct);

            var data = await response.Content.ReadFromJsonAsync<NewsResponse>(ct);
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
        return items;
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
