using System.Globalization;
using Microsoft.Extensions.Options;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Clients.WeatherApi;

internal sealed class WeatherApiClient(
    HttpClient httpClient,
    IOptions<WeatherApiOptions> options,
    ILogger<WeatherApiClient> logger) : IWeatherClient
{
    public string SourceName => "WeatherAPI";

    public async Task<ForecastSourceDto> GetForecastAsync(
        string city, string countryCode, DateOnly date, CancellationToken ct)
    {
        try
        {
            var response = await FetchForecastAsync(city, countryCode, ct);
            if (response?.Forecast.ForecastDay is null or { Count: 0 })
                return ForecastSourceDto.Failure(SourceName, "No forecast found for the requested city");

            var dateString = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var matchingDay = response.Forecast.ForecastDay
                .FirstOrDefault(d => string.Equals(d.Date, dateString, StringComparison.Ordinal));
            if (matchingDay is null)
                return ForecastSourceDto.Failure(SourceName, "No forecast found for the requested date");

            var avgFeelsLike = matchingDay.Hour.Count > 0
                ? Math.Round(matchingDay.Hour.Average(h => h.FeelsLikeC), 1)
                : matchingDay.Day.AvgTempC;

            return ForecastSourceDto.Success(SourceName, new ForecastDto
            {
                MaxTempC = matchingDay.Day.MaxTempC,
                MinTempC = matchingDay.Day.MinTempC,
                AvgTempC = matchingDay.Day.AvgTempC,
                Condition = matchingDay.Day.Condition.Text,
                Humidity = matchingDay.Day.AvgHumidity,
                WindSpeedKmh = matchingDay.Day.MaxWindKph,
                PrecipitationMm = matchingDay.Day.TotalPrecipMm,
                PrecipitationChance = matchingDay.Day.DailyChanceOfRain,
                AvgFeelsLikeC = avgFeelsLike
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch forecast from {Source}", SourceName);
            return ForecastSourceDto.Failure(SourceName, "Failed to fetch forecast");
        }
    }

    private async Task<WeatherApiForecastResponse?> FetchForecastAsync(string city, string countryCode, CancellationToken ct)
    {
        var apiKey = options.Value.ApiKey;
        var query = Uri.EscapeDataString($"{city},{countryCode}");

        using var httpResponse = await httpClient.GetAsync($"v1/forecast.json?key={apiKey}&q={query}&days=6", ct);

        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadFromJsonAsync<WeatherApiForecastResponse>(ct);
    }
}