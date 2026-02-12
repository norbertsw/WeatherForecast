# WeatherForecast API

A .NET 10 Minimal API microservice that aggregates weather forecasts from multiple providers, returning a unified response.

## Weather Providers

- [AccuWeather](https://developer.accuweather.com/core-weather/)
- [WeatherAPI](https://www.weatherapi.com/docs/)
- [Visual Crossing](https://www.visualcrossing.com/resources/documentation/weather-api/timeline-weather-api)

All providers are queried in parallel, and results are cached for 20 minutes.

## Tech Stack

- **.NET 10** - Minimal APIs
- **Docker** - Multi-stage build with distroless runtime
- **Azure** - App Service, Container Registry, Redis Cache, Key Vault, Application Insights
- **Bicep** - Infrastructure as Code
- **GitHub Actions** - CI/CD with OIDC authentication
- **xUnit v3 / Moq** - Unit & integration tests

## Architecture

Vertical slice / feature-folder pattern with parallel weather provider aggregation and distributed caching (in-memory locally, Redis in production).

```
Endpoint -> Validation -> Handler -> Parallel HTTP Clients -> Cache -> Response
```

## API Usage

```
GET /api/v1/weather/forecast?city=London&countryCode=GB&date=2026-02-12
Header: X-Api-Key: <your-key>
```

| Endpoint | Auth | Description |
|----------|------|-------------|
| `GET /healthz` | None | Health check |
| `GET /api/v1/weather/forecast` | `X-Api-Key` | Weather forecast |
