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