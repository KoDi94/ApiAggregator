using System.Text;
using ApiAggregator.Application.Services;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Endpoints;
using ApiAggregator.Infrastructure.Clients;
using ApiAggregator.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter just the token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddMemoryCache();
builder.Services.AddAuthorization();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };
    });

builder.Services.AddSingleton<IStatisticsService, StatisticsService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddScoped<IAggregationService, AggregationService>();
builder.Services.AddHostedService<PerformanceAnomalyService>();

builder.Services.AddHttpClient<OpenWeatherMapClient>(client =>
{
    var url = builder.Configuration["ExternalApis:OpenWeatherMap:BaseUrl"]
        ?? throw new InvalidOperationException("ExternalApis:OpenWeatherMap:BaseUrl is not configured");
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<OpenWeatherMapClient>());

builder.Services.AddHttpClient<NewsApiClient>(client =>
{
    var url = builder.Configuration["ExternalApis:NewsApi:BaseUrl"]
        ?? throw new InvalidOperationException("ExternalApis:NewsApi:BaseUrl is not configured");
    client.BaseAddress = new Uri(url);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<NewsApiClient>());

builder.Services.AddHttpClient<GitHubClient>(client =>
{
    var url = builder.Configuration["ExternalApis:GitHub:BaseUrl"]
        ?? throw new InvalidOperationException("ExternalApis:GitHub:BaseUrl is not configured");
    client.BaseAddress = new Uri(url);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<GitHubClient>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapAggregatedEndpoints();
app.MapStatisticsEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }
