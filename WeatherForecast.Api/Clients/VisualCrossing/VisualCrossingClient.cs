using System.Globalization;
using Microsoft.Extensions.Options;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Clients.VisualCrossing;

internal sealed class VisualCrossingClient(
    HttpClient httpClient,
    IOptions<VisualCrossingOptions> options,
    ILogger<VisualCrossingClient> logger) : IWeatherClient
{
    public string SourceName => "VisualCrossing";

    public async Task<ForecastSourceDto?> GetForecastAsync(string city, string countryCode, DateOnly date, CancellationToken ct)
    {
        try
        {
            var response = await FetchForecastAsync(city, countryCode, date, ct);

            var matchingDay = response?.Days.FirstOrDefault();
            if (matchingDay is null)
                return null;

            return new ForecastSourceDto
            {
                Source = SourceName,
                Forecast = new ForecastDto
                {
                    MaxTempC = matchingDay.Tempmax,
                    MinTempC = matchingDay.Tempmin,
                    AvgTempC = matchingDay.Temp,
                    Condition = matchingDay.Conditions,
                    Humidity = (int)matchingDay.Humidity,
                    WindSpeedKmh = matchingDay.Windspeed,
                    PrecipitationMm = matchingDay.Precip ?? 0,
                    PrecipitationChance = null,
                    AvgFeelsLikeC = null
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch forecast from {Source}", SourceName);
            return null;
        }
    }

    private async Task<VisualCrossingResponse?> FetchForecastAsync(string city, string countryCode, DateOnly date, CancellationToken ct)
    {
        var apiKey = options.Value.ApiKey;
        var location = Uri.EscapeDataString($"{city},{countryCode}");
        var dateString = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var httpResponse = await httpClient
            .GetAsync($"{location}/{dateString}?unitGroup=metric&include=days&key={apiKey}", ct);

        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadFromJsonAsync<VisualCrossingResponse>(ct);
    }
}