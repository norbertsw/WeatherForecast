namespace WeatherForecast.Api.Weather.Forecast;

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
    public required bool IsAvailable { get; init; }
    public ForecastDto? Forecast { get; init; }
    public string? ErrorMessage { get; init; }

    public static ForecastSourceDto Success(string source, ForecastDto forecast)
        => new() { Source = source, IsAvailable = true, Forecast = forecast };

    public static ForecastSourceDto Failure(string source, string errorMessage)
        => new() { Source = source, IsAvailable = false, ErrorMessage = errorMessage };
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