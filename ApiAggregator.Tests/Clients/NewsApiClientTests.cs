using System.Net;
using System.Text.Json;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Clients;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Tests.Clients;

public class NewsApiClientTests
{
    [Fact]
    public void Name_ReturnsNewsApi()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("NewsApi", client.Name);
    }

    [Fact]
    public void Category_ReturnsNews()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("News", client.Category);
    }

    [Fact]
    public async Task FetchAsync_ReturnsFallbackData_WhenApiKeyMissing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:NewsApi:ApiKey"] = ""
        }).Build();

        var http = new HttpClient(new FakeHandler("{}"));
        var client = new NewsApiClient(http, config);

        var result = await client.FetchAsync();

        Assert.True(result.Success);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task FetchAsync_ReturnsSuccessResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            articles = new[]
            {
                new
                {
                    title = "Test Headline",
                    description = "Test description",
                    url = "https://example.com",
                    publishedAt = "2024-06-01T00:00:00Z",
                    source = new { name = "Test Source" }
                }
            }
        });

        var client = CreateClientWithResponse(json);
        var result = await client.FetchAsync();

        Assert.True(result.Success);
        Assert.Equal("NewsApi", result.Source);
        Assert.NotEmpty(result.Data);
    }

    private static NewsApiClient CreateClientWithResponse(string json)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:NewsApi:ApiKey"] = "test-key",
            ["ExternalApis:NewsApi:Country"] = "us"
        }).Build();

        var http = new HttpClient(new FakeHandler(json));
        return new NewsApiClient(http, config);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _response;
        public FakeHandler(string response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_response) });
    }
}
