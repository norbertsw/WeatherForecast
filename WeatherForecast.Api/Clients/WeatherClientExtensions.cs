using System.Net.Http.Headers;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using WeatherForecast.Api.Clients.AccuWeather;
using WeatherForecast.Api.Clients.VisualCrossing;
using WeatherForecast.Api.Clients.WeatherApi;

namespace WeatherForecast.Api.Clients;

public static class WeatherClientExtensions
{
    public static IServiceCollection AddWeatherClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AccuWeatherOptions>()
            .Bind(configuration.GetSection(AccuWeatherOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<WeatherApiOptions>()
            .Bind(configuration.GetSection(WeatherApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<VisualCrossingOptions>()
            .Bind(configuration.GetSection(VisualCrossingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<AccuWeatherClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AccuWeatherOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            })
            .AddStandardResilienceHandler(SetTimeouts);

        services.AddHttpClient<WeatherApiClient>((sp, client) =>
            {
                var baseUrl = sp.GetRequiredService<IOptions<WeatherApiOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddStandardResilienceHandler(SetTimeouts);

        services.AddHttpClient<VisualCrossingClient>((sp, client) =>
            {
                var baseUrl = sp.GetRequiredService<IOptions<VisualCrossingOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddStandardResilienceHandler(SetTimeouts);

        services.AddTransient<IWeatherClient>(sp => sp.GetRequiredService<AccuWeatherClient>());
        services.AddTransient<IWeatherClient>(sp => sp.GetRequiredService<WeatherApiClient>());
        services.AddTransient<IWeatherClient>(sp => sp.GetRequiredService<VisualCrossingClient>());

        return services;
    }

    private static void SetTimeouts(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    }
}