using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Infrastructure.Clients;

public class GitHubClient(HttpClient http, IConfiguration config) : BaseApiClient(http)
{
    private const string EndpointPath = "search/repositories";
    private readonly string? _token = config["ExternalApis:GitHub:Token"];

    public override string Name => "GitHub";
    public override string Category => "Development";
    protected override bool HasCredentials => !string.IsNullOrEmpty(_token);

    protected override async Task<List<AggregatedItem>> FetchFromApiAsync(CancellationToken ct)
    {
        Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var url = $"{EndpointPath}?q=stars:>1000&sort=stars&order=desc&per_page=10";
        var response = await Http.GetAsync(url, ct);
        await EnsureSuccessOrThrowAsync(response, ct);

        var data = await response.Content.ReadFromJsonAsync<GitHubSearchResponse>(ct);
        var items = new List<AggregatedItem>();

        if (data?.Items != null)
        {
            foreach (var repo in data.Items)
            {
                items.Add(new AggregatedItem
                {
                    Id = $"github-{repo.Id}",
                    Title = repo.FullName ?? "(untitled)",
                    Description = repo.Description ?? "(no description)",
                    Category = "Development",
                    Source = Name,
                    Date = repo.CreatedAt,
                    Url = repo.HtmlUrl ?? "",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Stars"] = repo.StargazersCount,
                        ["Language"] = repo.Language ?? "Unknown",
                        ["Forks"] = repo.ForksCount
                    }
                });
            }
        }

        return items;
    }

    private class GitHubSearchResponse
    {
        [JsonPropertyName("items")] public GitHubRepo[]? Items { get; set; }
    }

    private class GitHubRepo
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("full_name")] public string? FullName { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
        [JsonPropertyName("stargazers_count")] public int StargazersCount { get; set; }
        [JsonPropertyName("forks_count")] public int ForksCount { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    }
}
