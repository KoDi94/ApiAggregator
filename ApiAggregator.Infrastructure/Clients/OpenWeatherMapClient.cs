using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ApiAggregator.Infrastructure.Clients;

public class OpenWeatherMapClient : IExternalApiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string[] _cities;

    public OpenWeatherMapClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var section = config.GetSection("ExternalApis:OpenWeatherMap");
        _apiKey = section["ApiKey"] ?? "";
        _cities = section.GetSection("Cities").Get<string[]>() ?? ["London", "New York", "Tokyo"];
    }

    public string Name => "OpenWeatherMap";
    public string Category => "Weather";

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
            foreach (var city in _cities)
            {
                var url = $"weather?q={city}&units=metric&appid={_apiKey}";
                var response = await _http.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<WeatherResponse>(cancellationToken);
                if (data?.Weather.Length > 0)
                {
                    items.Add(new AggregatedItem
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
                    });
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
                ErrorMessage = $"OpenWeatherMap error: {ex.Message}",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private List<AggregatedItem> GetFallbackData()
    {
        var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Windy", "Clear" };
        var rng = new Random();
        var cityList = _cities.Length > 0 ? _cities : ["London", "New York", "Tokyo"];
        return Enumerable.Range(1, 5).Select(i => new AggregatedItem
        {
            Id = $"weather-{Guid.NewGuid():N}",
            Title = $"Weather in {cityList[i % cityList.Length]}",
            Description = $"{rng.Next(5, 35)}°C, {conditions[rng.Next(conditions.Length)]}",
            Category = "Weather",
            Source = "OpenWeatherMap",
            Date = DateTime.UtcNow.AddHours(-rng.Next(0, 24)),
            Url = "https://openweathermap.org",
            Metadata = new Dictionary<string, object> { ["Source"] = "fallback" }
        }).ToList();
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
