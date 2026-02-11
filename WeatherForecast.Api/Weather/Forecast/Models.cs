using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace WeatherForecast.Api.Weather.Forecast;

public sealed record WeatherForecastRequest
{
    [FromQuery] public required string City { get; init; }

    [FromQuery]
    public required string CountryCode
    {
        get;
        init => field = value.ToUpperInvariant();
    }

    [FromQuery] public required DateOnly Date { get; init; }
}

public sealed record WeatherForecastResponse
{
    public required DateOnly Date { get; init; }
    public required LocationDto Location { get; init; }
    public required IReadOnlyCollection<ForecastSourceDto> Forecasts { get; init; }
    public required MetadataDto Metadata { get; init; }
}

public sealed record LocationDto
{
    public required string Name { get; init; }
    public required string CountryCode { get; init; }
}

public sealed record ForecastSourceDto
{
    public required string Source { get; init; }
    public required ForecastDto Forecast { get; init; }
}

public sealed record ForecastDto
{
    public required double MaxTempC { get; init; }
    public required double MinTempC { get; init; }
    public double? AvgTempC { get; init; }
    public required double? AvgFeelsLikeC { get; init; }
    public required string? Condition { get; init; }
    public required int Humidity { get; init; }
    public required double WindSpeedKmh { get; init; }
    public required double PrecipitationMm { get; init; }
    public int? PrecipitationChance { get; init; }
}

public sealed record MetadataDto
{
    public required DateTimeOffset GeneratedAt { get; init; }
}

public class ForecastRequestValidator : AbstractValidator<WeatherForecastRequest>
{
    public ForecastRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Date).NotNull().Must(date =>
            {
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);
                return date >= today && date <= today.AddDays(5);
            })
            .WithMessage("Date must be between today and 5 days from now");

        RuleFor(x => x.City).NotEmpty();
    }
}