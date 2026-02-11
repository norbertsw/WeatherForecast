using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.Clients.AccuWeather;

internal sealed class AccuWeatherClient(
    HttpClient httpClient,
    IDistributedCache cache,
    ILogger<AccuWeatherClient> logger) : IWeatherClient
{
    private static readonly DistributedCacheEntryOptions LocationCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
    };

    public string SourceName => "AccuWeather";

    public async Task<ForecastSourceDto?> GetForecastAsync(string city, string countryCode, DateOnly date,
        CancellationToken ct = default)
    {
        try
        {
            var locationKey = await GetLocationKeyAsync(city, countryCode, ct);
            if (locationKey is null)
                return null;

            var response = await FetchDailyForecastAsync(locationKey, ct);
            if (response?.DailyForecasts is null or { Count: 0 })
                return null;

            var dateString = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var matchingDay = response.DailyForecasts
                .FirstOrDefault(d => d.Date.StartsWith(dateString, StringComparison.Ordinal));

            return matchingDay is not null
                ? MapToForecastSource(matchingDay)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch forecast from {Source}", SourceName);
            return null;
        }
    }

    private ForecastSourceDto MapToForecastSource(AccuWeatherDailyForecast matchingDay)
    {
        var minTemp = matchingDay.Temperature.Minimum.Value;
        var maxTemp = matchingDay.Temperature.Maximum.Value;
        var avgTemp = (minTemp + maxTemp) / 2.0;
        var avgFeelsLike = (matchingDay.RealFeelTemperature.Minimum.Value + matchingDay.RealFeelTemperature.Maximum.Value) / 2.0;

        return new ForecastSourceDto
        {
            Source = SourceName,
            Forecast = new ForecastDto
            {
                MaxTempC = maxTemp,
                MinTempC = minTemp,
                AvgTempC = avgTemp,
                AvgFeelsLikeC = avgFeelsLike,
                Condition = matchingDay.Day?.IconPhrase,
                Humidity = matchingDay.Day?.RelativeHumidity.Average ?? 0,
                WindSpeedKmh = matchingDay.Day?.Wind?.Speed.Value ?? 0,
                PrecipitationMm = matchingDay.Day?.TotalLiquid?.Value ?? 0,
                PrecipitationChance = matchingDay.Day?.PrecipitationProbability
            }
        };
    }

    private async Task<AccuWeatherForecastResponse?> FetchDailyForecastAsync(string locationKey, CancellationToken ct)
    {
        using var httpResponse = await httpClient.GetAsync($"/forecasts/v1/daily/5day/{locationKey}?metric=true&details=true", ct);

        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadFromJsonAsync<AccuWeatherForecastResponse>(ct);
    }

    private async Task<string?> GetLocationKeyAsync(string city, string countryCode, CancellationToken ct)
    {
        var cacheKey = $"accu-loc:{city.ToUpperInvariant()}:{countryCode}";
        var cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
            return cached;

        using var httpResponse = await httpClient
            .GetAsync($"/locations/v1/cities/search?q={Uri.EscapeDataString(city)}&countryCode={Uri.EscapeDataString(countryCode)}", ct);

        httpResponse.EnsureSuccessStatusCode();
        var locations = await httpResponse.Content.ReadFromJsonAsync<AccuWeatherLocation[]>(ct);

        if (locations is null or { Length: 0 })
        {
            logger.LogWarning("AccuWeather location search returned no results for {City}, {CountryCode}", city, countryCode);
            return null;
        }

        var locationKey = locations[0].Key;
        await cache.SetStringAsync(cacheKey, locationKey, LocationCacheOptions, ct);

        return locationKey;
    }
}