using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using WeatherForecast.Api.Clients;

namespace WeatherForecast.Api.Weather.Forecast;

public static class ForecastEndpoint
{
    public static RouteGroupBuilder MapGetWeatherForecastEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/forecast",
            async Task<Results<Ok<WeatherForecastResponse>, ValidationProblem>> (
                [FromServices] IValidator<WeatherForecastRequest> validator,
                [FromServices] IDistributedCache cache,
                [FromServices] IEnumerable<IWeatherClient> weatherClients,
                [FromServices] ILoggerFactory loggerFactory,
                [FromServices] TimeProvider timeProvider,
                [AsParameters] WeatherForecastRequest weatherForecastRequest,
                CancellationToken ct) =>
            {
                var validationResult = await validator.ValidateAsync(weatherForecastRequest, ct);
                if (!validationResult.IsValid) return TypedResults.ValidationProblem(validationResult.ToDictionary());

                var result = await ForecastHandler.HandleAsync(
                    weatherForecastRequest, cache, weatherClients, timeProvider,
                    loggerFactory.CreateLogger(nameof(ForecastHandler)), ct);

                return TypedResults.Ok(result);
            });

        return group;
    }
}