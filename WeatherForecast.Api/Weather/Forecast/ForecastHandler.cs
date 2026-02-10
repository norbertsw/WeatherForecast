using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace WeatherForecast.Api.Weather.Forecast;

public static class ForecastHandler
{
    public static async Task<WeatherForecastResponse> HandleAsync(
        WeatherForecastRequest request,
        IDistributedCache cache)
    {
        var cacheKey = $"{request.City}:{request.CountryCode}:{request.Date:yyyy-MM-dd}";

        var cachedData = await cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<WeatherForecastResponse>(cachedData)!;
        }

        throw new NotImplementedException();

        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(string.Empty));
    }
}