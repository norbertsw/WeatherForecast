using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace WeatherForecast.Api.Weather.Forecast;

public static class ForecastEndpoint
{
    public static RouteGroupBuilder MapGetWeatherForecastEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/forecast",
            async Task<Results<Ok<WeatherForecastResponse>, ValidationProblem>> (
                [FromServices] IValidator<WeatherForecastRequest> validator,
                [FromServices] IDistributedCache cache,
                [AsParameters] WeatherForecastRequest weatherForecastRequest) =>
            {
                var validationResult = await validator.ValidateAsync(weatherForecastRequest);
                if (!validationResult.IsValid) return TypedResults.ValidationProblem(validationResult.ToDictionary());

                var result = await ForecastHandler.HandleAsync(weatherForecastRequest, cache);
                return TypedResults.Ok(result);
            });

        return group;
    }
}