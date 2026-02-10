using WeatherForecast.Api.Filters;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Weather;

public static class WeatherApi
{
    public static RouteGroupBuilder MapWeatherApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/weather")
            .RequireRateLimiting("fixed")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.WithTags("Weather");

        group.MapGetWeatherForecastEndpoint();

        return group;
    }
}