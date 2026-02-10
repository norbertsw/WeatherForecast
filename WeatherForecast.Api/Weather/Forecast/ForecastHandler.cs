using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WeatherForecast.Api.Clients;

namespace WeatherForecast.Api.Weather.Forecast;

public static class ForecastHandler
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(20)
    };

    public static async Task<WeatherForecastResponse> HandleAsync(
        WeatherForecastRequest request,
        IDistributedCache cache,
        IEnumerable<IWeatherClient> weatherClients,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct = default)
    {
        var cacheKey = $"{request.City}:{request.CountryCode}:{request.Date:yyyy-MM-dd}";

        var weatherForecastResponse = await RetrieveCachedWeatherForecast(cacheKey, cache, logger, ct);
        if (weatherForecastResponse is not null)
            return weatherForecastResponse;

        var apiResults = await FetchWeatherForecastDataAsync(request, weatherClients, ct);

        var response = new WeatherForecastResponse
        {
            Date = request.Date,
            Forecasts = apiResults,
            Location = new LocationDto
            {
                CountryCode = request.CountryCode,
                Name = request.City
            },
            Metadata = new MetadataDto
            {
                GeneratedAt = timeProvider.GetUtcNow()
            }
        };

        if (!apiResults.IsEmpty)
            await CacheForecastResponseAsync(response, cacheKey, cache, logger, ct);

        return response;
    }

    private static async Task CacheForecastResponseAsync(WeatherForecastResponse response, string cacheKey,
        IDistributedCache cache, ILogger logger, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                CacheOptions,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache forecast for {CacheKey}", cacheKey);
        }
    }

    private static async Task<ConcurrentBag<ForecastSourceDto>> FetchWeatherForecastDataAsync(WeatherForecastRequest request,
        IEnumerable<IWeatherClient> weatherClients,
        CancellationToken ct)
    {
        var apiResults = new ConcurrentBag<ForecastSourceDto>();
        await Parallel.ForEachAsync(weatherClients, ct, async (client, token) =>
        {
            var result = await client.GetForecastAsync(request.City, request.CountryCode, request.Date, token);
            if (result is not null)
                apiResults.Add(result);
        });

        return apiResults;
    }

    private static async Task<WeatherForecastResponse?> RetrieveCachedWeatherForecast(string cacheKey,
        IDistributedCache cache, ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var cachedData = await cache.GetStringAsync(cacheKey, ct);
            if (string.IsNullOrEmpty(cachedData))
                return null;

            var responseFromCache = JsonSerializer.Deserialize<WeatherForecastResponse>(cachedData);
            return responseFromCache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve cached forecast for {CacheKey}", cacheKey);
            return null;
        }
    }
}