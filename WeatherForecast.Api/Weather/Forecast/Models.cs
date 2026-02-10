using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace WeatherForecast.Api.Weather.Forecast;

public sealed record WeatherForecastRequest(
    [FromQuery] string City,
    [FromQuery] string CountryCode,
    [FromQuery] DateOnly Date);

public sealed record WeatherForecastResponse(
    string City,
    string Country,
    DateOnly Date,
    IReadOnlyList<ForecastResult> Forecasts);

public sealed record ForecastResult(
    string Source,
    double TemperatureCelsius,
    double TemperatureFeelsLikeCelsius,
    int Humidity,
    double WindSpeedKmh,
    double PrecipitationMm);

public class ForecastRequestValidator : AbstractValidator<WeatherForecastRequest>
{
    public ForecastRequestValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Date).NotNull();
        RuleFor(x => x.City).NotEmpty();
    }
}