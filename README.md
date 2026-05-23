# ApiAggregator

A .NET API aggregation service that consolidates data from multiple simulated external APIs and provides a unified endpoint with caching, filtering, sorting, pagination, JWT authentication, performance statistics, and anomaly detection.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        ApiAggregator.Web                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ MinAPI   │  │ Auth     │  │ Stats    │  │ Aggregation   │  │
│  │ Endpoints│  │ Endpoints│  │ Endpoints│  │ Service       │  │
│  └────┬─────┘  └──────────┘  └──────────┘  └───────┬───────┘  │
│       │                                            │          │
│  ┌────▼─────────────────────────────────────────────▼───────┐  │
│  │                    Polly Resilience                       │  │
│  │               (Retry + Timeout Pipeline)                  │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ Weather  │  │  News    │  │  GitHub  │  │ IMemoryCache  │  │
│  │ ApiClient│  │ ApiClient│  │ ApiClient│  │ (CacheService)│  │
│  └──────────┘  └──────────┘  └──────────┘  └───────────────┘  │
│                           │                                    │
│  ┌────────────────────────▼────────────────────────────────┐   │
│  │              StatisticsService                           │   │
│  │  (Per-API: request count, avg time, performance buckets) │   │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │     PerformanceAnomalyService (BackgroundService)         │  │
│  │  Checks every 60s: if last 5min avg > 50% of overall avg │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Description |
|---------|-------------|
| `ApiAggregator` | ASP.NET Core Web API (Minimal API pattern) |
| `ApiAggregator.Domain` | Class library with models and interfaces |
| `ApiAggregator.Tests` | xUnit test project |

## API Endpoints

### POST `/api/auth/token`

Returns a JWT bearer token for accessing protected endpoints.

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expires": "2026-05-23T12:00:00Z"
}
```

### GET `/api/aggregated`

Returns aggregated data from all external API sources. **Requires JWT auth.**

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `category` | string | - | Filter by category: `Weather`, `News`, `GitHub` |
| `search` | string | - | Search term (matches title and description) |
| `fromDate` | datetime | - | Filter items after this date |
| `toDate` | datetime | - | Filter items before this date |
| `sortBy` | string | `date` | Sort field: `date`, `title`, `category` |
| `sortOrder` | string | `asc` | Sort order: `asc`, `desc` |
| `page` | int | `1` | Page number |
| `pageSize` | int | `20` | Items per page |

**Response:**
```json
{
  "items": [
    {
      "id": "weather-abc123",
      "title": "Weather in London",
      "description": "Temperature: 22°C, Condition: Sunny",
      "category": "Weather",
      "source": "Weather",
      "date": "2026-05-22T10:00:00Z",
      "url": "https://weather.example.com/London",
      "metadata": { "City": "London", "TemperatureC": 22, "Condition": "Sunny" }
    }
  ],
  "totalCount": 30,
  "page": 1,
  "pageSize": 20,
  "sources": ["Weather", "News", "GitHub"],
  "errors": []
}
```

### GET `/api/statistics`

Returns request statistics per API. **Requires JWT auth.**

**Response:**
```json
[
  {
    "apiName": "Weather",
    "totalRequests": 10,
    "averageResponseTimeMs": 152.3,
    "buckets": { "fast": 4, "average": 4, "slow": 2 }
  }
]
```

Performance bucket thresholds:
- **Fast**: < 100ms
- **Average**: 100ms – 250ms
- **Slow**: > 250ms

## Setup & Configuration

### Prerequisites

- .NET 8.0 SDK

### Build & Run

```bash
dotnet build
dotnet run --project ApiAggregator
```

The API starts at `http://localhost:5166` or `https://localhost:7054`. Swagger UI is available at `/swagger`.

### Run Tests

```bash
dotnet test
```

### Configuration (`appsettings.json`)

```json
{
  "Jwt": {
    "Key": "YourSecretKeyHere_Min32Characters",
    "Issuer": "ApiAggregator",
    "Audience": "ApiAggregatorClients"
  },
  "Cache": {
    "DefaultExpirationMinutes": 5
  },
  "PerformanceBuckets": {
    "FastThresholdMs": 100,
    "SlowThresholdMs": 250
  }
}
```

## Usage Example

```bash
# 1. Get a token
TOKEN=$(curl -s -X POST http://localhost:5166/api/auth/token | \
  python -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 2. Get aggregated data
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5166/api/aggregated?category=News&sortBy=date&sortOrder=desc&pageSize=3"

# 3. Get statistics
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5166/api/statistics
```

## Simulated Data Sources

The service includes three simulated external API clients:

| Client | Category | Data |
|--------|----------|------|
| `WeatherApiClient` | Weather | 10 weather forecasts for random cities |
| `NewsApiClient` | News | 10 news articles with topics/sources |
| `GitHubApiClient` | GitHub | 10 trending repositories |

Each client simulates a random delay (50–400ms) to demonstrate parallel fetching and caching.

## Key Features

- **Parallel Execution**: All API clients are called simultaneously via `Task.WhenAll`
- **Caching**: `IMemoryCache` with configurable TTL (default 5 min)
- **Resilience**: Polly retry policy (2 retries, exponential backoff) + 10s timeout
- **Graceful Degradation**: If one API fails, partial results from others are returned
- **Performance Statistics**: Per-API request count, avg response time, performance buckets
- **Anomaly Detection**: Background service logs warnings when last-5-min avg exceeds overall avg by 50%
- **JWT Auth**: Bearer token authentication on data endpoints
- **Filtering & Sorting**: By category, search term, date range; sort by date/title/category
- **Pagination**: Configurable page size

## Extending with a New API

1. Create a new class implementing `IExternalApiClient` in `ApiAggregator/Clients/`
2. Register it in `Program.cs`: `builder.Services.AddScoped<IExternalApiClient, YourApiClient>();`
3. The `AggregationService` automatically discovers and includes it.
