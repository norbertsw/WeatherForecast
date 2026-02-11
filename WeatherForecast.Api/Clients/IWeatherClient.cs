using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Clients;

public interface IWeatherClient
{
    string SourceName { get; }

    Task<ForecastSourceDto?> GetForecastAsync(
        string city,
        string countryCode,
        DateOnly date,
        CancellationToken ct = default);
}
