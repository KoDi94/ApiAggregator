using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Domain.Models;
using System.Diagnostics;
using System.Text.Json;

namespace ApiAggregator.Infrastructure.Clients;

public abstract class BaseApiClient(HttpClient http) : IExternalApiClient
{
    protected readonly HttpClient Http = http;

    public abstract string Name { get; }
    public abstract string Category { get; }
    protected abstract bool HasCredentials { get; }
    protected abstract Task<List<AggregatedItem>> FetchFromApiAsync(CancellationToken ct);

    public async Task<ApiClientResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!HasCredentials)
        {
            sw.Stop();
            return Error($"{Name} API key not configured", sw.ElapsedMilliseconds);
        }

        try
        {
            var items = await FetchFromApiAsync(cancellationToken);
            sw.Stop();
            return Ok(items, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Error($"{Name} error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    protected async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var detail = ParseErrorBody(body);
            var code = (int)response.StatusCode;
            var msg = string.IsNullOrEmpty(detail) ? $"{code} {response.ReasonPhrase}" : $"{code} - {detail}";
            throw new HttpRequestException(msg, null, response.StatusCode);
        }
    }

    protected static string? TryGetErrorField(string body, params string[] fields)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var field in fields)
            {
                if (doc.RootElement.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
        }
        catch { }
        return null;
    }

    protected virtual string ParseErrorBody(string body)
    {
        var code = TryGetErrorField(body, "code", "cod", "error");
        var message = TryGetErrorField(body, "message", "Message", "error_description");
        if (code != null && message != null)
            return $"{code} - {message}";
        return message ?? code ?? "";
    }

    private ApiClientResult Ok(List<AggregatedItem> data, long ms)
    {
        return new ApiClientResult { Success = true, Source = Name, Data = data, ResponseTimeMs = ms };
    }

    private ApiClientResult Error(string message, long ms)
    {
        return new ApiClientResult { Success = false, Source = Name, ErrorMessage = message, ResponseTimeMs = ms };
    }
}
