using Asp.Versioning;
using WeatherForecast.Api.Filters;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Weather;

public static class WeatherApi
{
    public static WebApplication MapWeatherApi(this WebApplication app)
    {
        var v1Api = new ApiVersion(1, 0);
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(v1Api)
            .Build();

        var group = app.MapGroup("/v{version:apiVersion}/weather")
            .RequireRateLimiting("fixed")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(v1Api);

        group.WithTags("Weather");

        group.MapGetWeatherForecastEndpoint();

        return app;
    }
}