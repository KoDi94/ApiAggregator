using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Infrastructure.Clients;

public class OpenWeatherMapClient : BaseApiClient
{
    private const string EndpointPath = "weather";
    private readonly string _apiKey;
    private readonly string[] _cities;

    public OpenWeatherMapClient(HttpClient http, IConfiguration config) : base(http)
    {
        var section = config.GetSection("ExternalApis:OpenWeatherMap");
        _apiKey = section["ApiKey"] ?? "";
        _cities = section.GetSection("Cities").Get<string[]>() ?? ["London", "New York", "Tokyo"];
    }

    public override string Name => "OpenWeatherMap";
    public override string Category => "Weather";
    protected override bool HasCredentials => !string.IsNullOrEmpty(_apiKey);

    protected override async Task<List<AggregatedItem>> FetchFromApiAsync(CancellationToken ct)
    {
        var tasks = _cities.Select(async city =>
        {
            var url = $"{EndpointPath}?q={city}&units=metric&appid={_apiKey}";
            var response = await Http.GetAsync(url, ct);
            await EnsureSuccessOrThrowAsync(response, ct);

            var data = await response.Content.ReadFromJsonAsync<WeatherResponse>(ct);
            if (data?.Weather.Length > 0)
            {
                return new AggregatedItem
                {
                    Id = $"weather-{Guid.NewGuid():N}",
                    Title = $"Weather in {data.Name}",
                    Description = $"{data.Main.Temp}°C, {data.Weather[0].Description}",
                    Category = "Weather",
                    Source = Name,
                    Date = DateTime.UtcNow,
                    Url = $"https://openweathermap.org/city/{data.Id}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["City"] = data.Name,
                        ["TemperatureC"] = data.Main.Temp,
                        ["Humidity"] = data.Main.Humidity,
                        ["Condition"] = data.Weather[0].Description
                    }
                };
            }
            return null;
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).ToList()!;
    }

    private class WeatherResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("main")] public MainData Main { get; set; } = new();
        [JsonPropertyName("weather")] public WeatherData[] Weather { get; set; } = [];
    }

    private class MainData
    {
        [JsonPropertyName("temp")] public double Temp { get; set; }
        [JsonPropertyName("humidity")] public int Humidity { get; set; }
    }

    private class WeatherData
    {
        [JsonPropertyName("description")] public string Description { get; set; } = "";
    }
}
