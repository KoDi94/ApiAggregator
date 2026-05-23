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

builder.Services.AddHttpClient<IExternalApiClient, OpenWeatherMapClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:OpenWeatherMap:BaseUrl"]
        ?? "https://api.openweathermap.org/data/2.5");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IExternalApiClient, NewsApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:NewsApi:BaseUrl"]
        ?? "https://newsapi.org/v2");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IExternalApiClient, GitHubClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:GitHub:BaseUrl"]
        ?? "https://api.github.com");
    client.Timeout = TimeSpan.FromSeconds(10);
});

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
