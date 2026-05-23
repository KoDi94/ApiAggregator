using System.Net;
using System.Text.Json;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Clients;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Tests.Clients;

public class OpenWeatherMapClientTests
{
    [Fact]
    public void Name_ReturnsOpenWeatherMap()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("OpenWeatherMap", client.Name);
    }

    [Fact]
    public void Category_ReturnsWeather()
    {
        var client = CreateClientWithResponse("{}");
        Assert.Equal("Weather", client.Category);
    }

    [Fact]
    public async Task FetchAsync_ReturnsError_WhenApiKeyMissing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:OpenWeatherMap:ApiKey"] = ""
        }).Build();

        var http = new HttpClient(new FakeHandler("{}"));
        var client = new OpenWeatherMapClient(http, config);

        var result = await client.FetchAsync();

        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task FetchAsync_ReturnsSuccessResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            id = 2643743, name = "London",
            main = new { temp = 15.5, humidity = 72 },
            weather = new[] { new { description = "clear sky" } }
        });

        var client = CreateClientWithResponse(json);
        var result = await client.FetchAsync();

        Assert.True(result.Success);
        Assert.Equal("OpenWeatherMap", result.Source);
        Assert.NotEmpty(result.Data);
    }

    private static OpenWeatherMapClient CreateClientWithResponse(string json)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:OpenWeatherMap:ApiKey"] = "test-key",
            ["ExternalApis:OpenWeatherMap:Cities:0"] = "London"
        }).Build();

        var http = new HttpClient(new FakeHandler(json)) { BaseAddress = new Uri("https://api.openweathermap.org/data/2.5") };
        return new OpenWeatherMapClient(http, config);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _response;
        public FakeHandler(string response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_response) });
    }
}
