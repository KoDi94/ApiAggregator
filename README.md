# ApiAggregator

A .NET API aggregation service that consolidates data from multiple real external APIs (OpenWeatherMap, NewsApi, GitHub) into a unified endpoint with caching, filtering, sorting, pagination, JWT authentication, performance statistics, and anomaly detection.

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                      ApiAggregator (Web)                             │
│  ┌──────────────┐  ┌──────────┐  ┌──────────┐  ┌────────────────┐  │
│  │ Aggregated   │  │ Auth     │  │ Stats    │  │ Swagger        │  │
│  │ Endpoints    │  │ Endpoints│  │ Endpoints│  │ (JWT Security) │  │
│  └──────┬───────┘  └──────────┘  └──────────┘  └────────────────┘  │
│         │                                                           │
│  ┌──────▼────────────────────────────────────────────────────────┐  │
│  │                 ApiAggregator.Application                       │  │
│  │  ┌──────────────────────────────────────────────────────────┐  │  │
│  │  │                AggregationService                         │  │  │
│  │  │  (Polly pipeline: 2 retries, exp backoff, 10s timeout)    │  │  │
│  │  │  (Pre-filters clients by source, cache check, merge)      │  │  │
│  │  └──────────────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────────────-┘  │
│         │                                                           │
│  ┌──────▼────────────────────────────────────────────────────────┐  │
│  │              ApiAggregator.Infrastructure                       │  │
│  │  ┌──────────┐  ┌──────────┐  ┌─────────────────────────────┐  │  │
│  │  │Weather   │  │ NewsApi  │  │ GitHubClient                │  │  │
│  │  │Client    │  │ Client   │  │ (Bearer token, UserAgent)   │  │  │
│  │  │(parallel)│  │(1 call)  │  │                             │  │  │
│  │  └────┬─────┘  └────┬─────┘  └─────────────┬───────────────┘  │  │
│  │       │             │                       │                   │  │
│  │  ┌────▼─────────────▼───────────────────────▼───────────────┐  │  │
│  │  │              BaseApiClient (abstract)                     │  │  │
│  │  │  (Stopwatch, try/catch, EnsureSuccessOrThrowAsync,       │  │  │
│  │  │   ParseErrorBody, HasCredentials check)                   │  │  │
│  │  └──────────────────────────────────────────────────────────┘  │  │
│  │  ┌──────────────────────────┐  ┌────────────────────────────┐  │  │
│  │  │ CacheService             │  │ StatisticsService          │  │  │
│  │  │ (IMemoryCache, 10s TTL) │  │ (ReaderWriterLockSlim)     │  │  │
│  │  └──────────────────────────┘  └────────────────────────────┘  │  │
│  │  ┌──────────────────────────────────────────────────────────┐  │  │
│  │  │ PerformanceAnomalyService (BackgroundService)             │  │  │
│  │  │ Checks every 60s: if last 5min avg > 50% of overall avg  │  │  │
│  │  └──────────────────────────────────────────────────────────┘  │  │
│  │  ┌──────────────────────────────────────────────────────────┐  │  │
│  │  │ ServiceCollectionExtensions                                │  │  │
│  │  │ (Fluent extension methods for DI registration)             │  │  │
│  │  └──────────────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────────────-┘  │
└──────────────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Description |
|---------|-------------|
| `ApiAggregator` | ASP.NET Core Web API (Minimal API pattern, Swagger, JWT auth) |
| `ApiAggregator.Application` | Application services (`AggregationService`, Polly pipeline) |
| `ApiAggregator.Domain` | Models, interfaces, DTOs (Clean Architecture inner layer) |
| `ApiAggregator.Infrastructure` | API clients, caching, statistics, anomaly detection, DI extensions |
| `ApiAggregator.Tests` | xUnit + Moq tests (40 tests: unit + integration) |

## External APIs

| Client | Category | Implementation | Data |
|--------|----------|----------------|------|
| `OpenWeatherMapClient` | Weather | 5 cities fetched in parallel via `Task.WhenAll` | Temp, humidity, condition per city |
| `NewsApiClient` | News | Single `top-headlines` call, `pageSize=20` | Top headlines across all categories |
| `GitHubClient` | Development | Search repos, `stars:>1000`, `per_page=10` | Repo name, stars, language, forks |

All clients extend `BaseApiClient` which provides:
- `HasCredentials` check — returns error if API key is missing
- `EnsureSuccessOrThrowAsync` — reads error response body and parses provider-specific error codes
- `ParseErrorBody` — extracts `code`/`message` from JSON error responses
- Automatic `Stopwatch` timing and error wrapping

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
| `source` | string | - | Filter by source name: `OpenWeatherMap`, `NewsApi`, `GitHub` (pre-filters which clients are called) |
| `id` | string | - | Filter by item ID |
| `category` | string | - | Filter by category: `Weather`, `News`, `Development` |
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
      "description": "22°C, clear sky",
      "category": "Weather",
      "source": "OpenWeatherMap",
      "date": "2026-05-22T10:00:00Z",
      "url": "https://openweathermap.org/city/123",
      "metadata": { "City": "London", "TemperatureC": 22, "Humidity": 50, "Condition": "clear sky" }
    }
  ],
  "totalCount": 35,
  "page": 1,
  "pageSize": 20,
  "sources": ["OpenWeatherMap", "NewsApi", "GitHub"],
  "errors": []
}
```

**Cache header:** `X-Cache` response header indicates `MISS` or comma-separated source names served from cache.

### GET `/api/statistics`

Returns request statistics per API. **Requires JWT auth.**

**Response:**
```json
[
  {
    "apiName": "OpenWeatherMap",
    "totalRequests": 5,
    "averageResponseTimeMs": 62.3,
    "buckets": { "fast": 4, "average": 1, "slow": 0 }
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

### API Keys

Keys are stored via `dotnet user-secrets` (never committed to git):

```bash
dotnet user-secrets set "ExternalApis:OpenWeatherMap:ApiKey" "your-key" --project ApiAggregator
dotnet user-secrets set "ExternalApis:NewsApi:ApiKey" "your-key" --project ApiAggregator
dotnet user-secrets set "ExternalApis:GitHub:Token" "your-token" --project ApiAggregator
```

- **OpenWeatherMap**: [free API key](https://openweathermap.org/api)
- **NewsApi**: [free API key](https://newsapi.org/register)
- **GitHub**: [personal access token](https://github.com/settings/tokens) (no scopes needed for public repos)

Missing keys return `{ "success": false, "errorMessage": "OpenWeatherMap API key not configured" }` — no fallback data.

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
    "Key": "ThisIsASecureKeyForDevelopmentOnly_ChangeInProduction!",
    "Issuer": "ApiAggregator",
    "Audience": "ApiAggregatorClients"
  },
  "Cache": {
    "DefaultExpirationSeconds": 10
  },
  "ExternalApis": {
    "OpenWeatherMap": {
      "BaseUrl": "https://api.openweathermap.org/data/2.5/",
      "ApiKey": "",
      "Cities": [ "London", "New York", "Tokyo", "Paris", "Sydney" ]
    },
    "NewsApi": {
      "BaseUrl": "https://newsapi.org/v2/",
      "ApiKey": "",
      "Country": "us"
    },
    "GitHub": {
      "BaseUrl": "https://api.github.com/",
      "Token": ""
    }
  }
}
```

API keys in `appsettings.json` must be empty — they're set via `dotnet user-secrets`. `BaseUrl` values must have trailing slashes to avoid HttpClient path segment replacement.

## Usage Example

```bash
# 1. Get a token
TOKEN=$(curl -s -X POST http://localhost:5166/api/auth/token | \
  python -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 2. Get aggregated data (filter by source, sorted by date)
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5166/api/aggregated?source=OpenWeatherMap&sortBy=date&sortOrder=desc&pageSize=3"

# 3. Get statistics
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5166/api/statistics
```

## Key Features

- **Real External APIs**: OpenWeatherMap, NewsApi, GitHub — no simulated data
- **Parallel Execution**: All API clients called simultaneously via `Task.WhenAll`; OpenWeatherMap city requests also parallelized
- **Caching**: `IMemoryCache` with configurable TTL (default 10s), `X-Cache` response header
- **Resilience**: Polly retry policy (2 retries, exponential backoff) + 10s timeout per client
- **Partial Results**: If one API fails, success/error details are returned together — never a 500
- **Error Parsing**: Reads API error response bodies and extracts provider-specific codes (`400 - userAgentMissing - ...`)
- **Performance Statistics**: Per-API request count, avg response time, performance buckets (`ReaderWriterLockSlim`)
- **Anomaly Detection**: Background service logs warnings when last-5-min avg exceeds overall avg by > 50%
- **JWT Auth**: Bearer token authentication on data endpoints, Swagger "Authorize" button
- **Filtering & Sorting**: By source, id, category, search term, date range; sort by date/title/category
- **Pagination**: Configurable page and page size
- **Clean Architecture**: Domain → Application → Infrastructure → Web, dependency inversion throughout
- **Fluent DI Registration**: `ServiceCollectionExtensions` with named methods (`AddOpenWeatherMapClient`, `AddJwtAuthentication`, etc.)

## Extending with a New API

1. Create `Infrastructure/Clients/NewApiClient.cs : BaseApiClient`
2. Override `Name`, `Category`, `HasCredentials`, `FetchFromApiAsync`
3. Register in `Program.cs` via the extension method pattern:
   ```csharp
   builder.Services.AddHttpClient<NewApiClient>(client => { ... });
   builder.Services.AddScoped<IExternalApiClient>(sp => sp.GetRequiredService<NewApiClient>());
   ```
4. Add config section in `appsettings.json` with `BaseUrl` (trailing slash) and `ApiKey`/`Token`
5. The `AggregationService` automatically discovers it via `IEnumerable<IExternalApiClient>`
