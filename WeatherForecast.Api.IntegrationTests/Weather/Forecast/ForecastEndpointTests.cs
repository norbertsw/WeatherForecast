using System.Net;
using System.Net.Http.Json;
using Shouldly;
using WeatherForecast.Api.IntegrationTests.Infrastructure;
using WeatherForecast.Api.Weather.Forecast;
using Xunit;

namespace WeatherForecast.Api.IntegrationTests.Weather.Forecast;

public class ForecastEndpointTests : IClassFixture<WeatherForecastApiFactory>
{
    private readonly HttpClient _client;

    public ForecastEndpointTests(WeatherForecastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string BuildForecastUrl(string city = "London", string countryCode = "GB", DateOnly? date = null)
    {
        var forecastDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return $"/api/v1/weather/forecast?city={city}&countryCode={countryCode}&date={forecastDate:yyyy-MM-dd}";
    }

    [Fact]
    public async Task GetForecast_WithValidRequest_Returns200WithForecasts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new HttpRequestMessage(HttpMethod.Get, BuildForecastUrl());
        request.Headers.Add("X-Api-Key", "test-api-key");

        // Act
        var response = await _client.SendAsync(request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WeatherForecastResponse>(ct);
        body.ShouldNotBeNull();
        body.Forecasts.ShouldNotBeEmpty();
        body.Location.Name.ShouldBe("London");
        body.Location.CountryCode.ShouldBe("GB");
    }

    [Fact]
    public async Task GetForecast_WithoutApiKey_Returns401()
    {
        // Arrange & Act
        var response = await _client.GetAsync(BuildForecastUrl(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetForecast_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, BuildForecastUrl());
        request.Headers.Add("X-Api-Key", "wrong-key");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetForecast_WithInvalidCountryCode_ReturnsValidationProblem()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, BuildForecastUrl(countryCode: "GBR"));
        request.Headers.Add("X-Api-Key", "test-api-key");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetForecast_WithDateTooFarInFuture_ReturnsValidationProblem()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var request = new HttpRequestMessage(HttpMethod.Get, BuildForecastUrl(date: futureDate));
        request.Headers.Add("X-Api-Key", "test-api-key");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetForecast_WithDateInPast_ReturnsValidationProblem()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var request = new HttpRequestMessage(HttpMethod.Get, BuildForecastUrl(date: futureDate));
        request.Headers.Add("X-Api-Key", "test-api-key");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetForecast_WithMissingCity_ReturnsValidationProblem()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/weather/forecast?city=&countryCode=GB&date={today:yyyy-MM-dd}");
        request.Headers.Add("X-Api-Key", "test-api-key");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}