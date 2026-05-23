using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiAggregator.Domain.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiAggregator.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthToken_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/token", null);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", content);
    }

    [Fact]
    public async Task AggregatedEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/aggregated?pageSize=5");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StatisticsEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/statistics");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AggregatedEndpoint_ReturnsData_WithValidToken()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/aggregated?pageSize=5");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<AggregatedResponse>(JsonOptions);
        Assert.NotNull(content);
        Assert.NotEmpty(content.Items);
        Assert.Equal(3, content.Sources.Count);
    }

    [Fact]
    public async Task AggregatedEndpoint_FiltersByCategory()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/aggregated?category=Weather");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<AggregatedResponse>(JsonOptions);
        Assert.NotNull(content);
        Assert.All(content.Items, item => Assert.Equal("Weather", item.Category));
    }

    [Fact]
    public async Task StatisticsEndpoint_ReturnsStats_WithValidToken()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.GetAsync("/api/aggregated?pageSize=1");

        var response = await client.GetAsync("/api/statistics");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<List<ApiStatistics>>(JsonOptions);
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    private async Task<string> GetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/auth/token", null);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("token").GetString()!;
    }
}
