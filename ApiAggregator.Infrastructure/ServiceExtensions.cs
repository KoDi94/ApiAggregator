using System.Text;
using ApiAggregator.Application.Services;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Clients;
using ApiAggregator.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ApiAggregator.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddAggregationServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddScoped<IAggregationService, AggregationService>();
        services.AddHostedService<PerformanceAnomalyService>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, string key, string issuer, string audience)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(audience);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddExternalApisFromConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var openWeatherUrl = configuration["ExternalApis:OpenWeatherMap:BaseUrl"]
            ?? throw new InvalidOperationException("ExternalApis:OpenWeatherMap:BaseUrl is not configured");
        services.AddOpenWeatherMapClient(openWeatherUrl);

        var newsApiUrl = configuration["ExternalApis:NewsApi:BaseUrl"]
            ?? throw new InvalidOperationException("ExternalApis:NewsApi:BaseUrl is not configured");
        services.AddNewsApiClient(newsApiUrl);

        var gitHubUrl = configuration["ExternalApis:GitHub:BaseUrl"]
            ?? throw new InvalidOperationException("ExternalApis:GitHub:BaseUrl is not configured");
        services.AddGitHubClient(gitHubUrl);

        return services;
    }

    public static IServiceCollection AddJwtFromConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
        var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured");
        var audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured");
        return services.AddJwtAuthentication(key, issuer, audience);
    }

    private static IServiceCollection AddOpenWeatherMapClient(this IServiceCollection services, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        services.AddHttpClient<OpenWeatherMapClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<OpenWeatherMapClient>());
        return services;
    }

    private static IServiceCollection AddNewsApiClient(this IServiceCollection services, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        services.AddHttpClient<NewsApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<NewsApiClient>());
        return services;
    }

    private static IServiceCollection AddGitHubClient(this IServiceCollection services, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        services.AddHttpClient<GitHubClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<GitHubClient>());
        return services;
    }
}
