using System.Net;
using System.Text.Json;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Clients;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Tests.Clients;

public class GitHubClientTests
{
    [Fact]
    public void Name_ReturnsGitHub()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("GitHub", client.Name);
    }

    [Fact]
    public void Category_ReturnsDevelopment()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("Development", client.Category);
    }

    [Fact]
    public async Task FetchAsync_ReturnsError_WhenTokenMissing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:GitHub:Token"] = ""
        }).Build();

        var http = new HttpClient(new FakeHandler("{}"));
        var client = new GitHubClient(http, config);

        var result = await client.FetchAsync();

        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task FetchAsync_ReturnsSuccessResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            items = new[]
            {
                new
                {
                    id = 12345,
                    full_name = "dotnet/runtime",
                    description = "Cross-platform runtime for .NET",
                    html_url = "https://github.com/dotnet/runtime",
                    language = "C#",
                    stargazers_count = 15000,
                    forks_count = 5000,
                    created_at = "2024-01-15T00:00:00Z"
                }
            }
        });

        var client = CreateClientWithResponse(json, withToken: true);
        var result = await client.FetchAsync();

        Assert.True(result.Success);
        Assert.Equal("GitHub", result.Source);
        Assert.NotEmpty(result.Data);
    }

    private static GitHubClient CreateClientWithResponse(string json, bool withToken = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:GitHub:Token"] = withToken ? "test-token" : ""
        }).Build();

        var http = new HttpClient(new FakeHandler(json)) { BaseAddress = new Uri("https://api.github.com") };
        return new GitHubClient(http, config);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _response;

        public FakeHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(_response) });
        }
    }
}
