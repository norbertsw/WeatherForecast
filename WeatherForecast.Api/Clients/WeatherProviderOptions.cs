using System.ComponentModel.DataAnnotations;

namespace WeatherForecast.Api.Clients;

public sealed class AccuWeatherOptions
{
    public const string SectionName = "WeatherProviders:AccuWeather";
    [Required] public required string ApiKey { get; init; }
    [Required] public required string BaseUrl { get; init; }
}

public sealed class WeatherApiOptions
{
    public const string SectionName = "WeatherProviders:WeatherApi";
    [Required] public required string ApiKey { get; init; }
    [Required] public required string BaseUrl { get; init; }
}

public sealed class VisualCrossingOptions
{
    public const string SectionName = "WeatherProviders:VisualCrossing";
    [Required] public required string ApiKey { get; init; }
    [Required] public required string BaseUrl { get; init; }
}