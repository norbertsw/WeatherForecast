using AutoFixture;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WeatherForecast.Api.Clients;
using WeatherForecast.Api.Weather.Forecast;

namespace WeatherForecast.Api.IntegrationTests.Infrastructure;

public sealed class WeatherForecastApiFactory : WebApplicationFactory<Program>
{
    public IWeatherClient MockWeatherClient { get; } = CreateDefaultMockClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KeyVaultUri", "");

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = "test-api-key",
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/",
                ["WeatherProviders:AccuWeather:ApiKey"] = "test-accu-key",
                ["WeatherProviders:AccuWeather:BaseUrl"] = "https://fake-accu.test/",
                ["WeatherProviders:WeatherApi:ApiKey"] = "test-weatherapi-key",
                ["WeatherProviders:WeatherApi:BaseUrl"] = "https://fake-weatherapi.test/",
                ["WeatherProviders:VisualCrossing:ApiKey"] = "test-vc-key",
                ["WeatherProviders:VisualCrossing:BaseUrl"] = "https://fake-vc.test/"
            });
        });

        builder.ConfigureServices(services =>
        {
            var weatherClientDescriptors = services
                .Where(d => d.ServiceType == typeof(IWeatherClient))
                .ToList();
            foreach (var descriptor in weatherClientDescriptors)
                services.Remove(descriptor);

            services.AddSingleton(MockWeatherClient);
        });
    }

    private static IWeatherClient CreateDefaultMockClient()
    {
        var fixture = new Fixture();
        var forecastSourceDto = fixture.Build<ForecastSourceDto>()
            .With(x => x.Source, "MockProvider")
            .Create();

        var client = new Mock<IWeatherClient>();
        client.Setup(x => x.SourceName).Returns("MockProvider");
        client.Setup(x => x.GetForecastAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecastSourceDto);

        return client.Object;
    }
}