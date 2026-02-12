using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;
using WeatherForecast.Api.Clients;
using WeatherForecast.Api.Clients.WeatherApi;
using Xunit;

namespace WeatherForecast.Api.Tests.Clients.WeatherApi;

public class WeatherApiClientTests
{
    private readonly IFixture _fixture;
    private readonly Mock<HttpMessageHandler> _handler;
    private readonly WeatherApiClient _sut;
    private readonly DateOnly _date;
    private readonly string _apiKey;

    public WeatherApiClientTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _handler = new Mock<HttpMessageHandler>();
        _apiKey = _fixture.Create<string>();

        var options = Options.Create(new WeatherApiOptions
        {
            ApiKey = _apiKey,
            BaseUrl = "https://api.weatherapi.com/"
        });
        var httpClient = new HttpClient(_handler.Object)
        {
            BaseAddress = new Uri("https://api.weatherapi.com/")
        };
        _sut = new WeatherApiClient(httpClient, options, NullLogger<WeatherApiClient>.Instance);
    }

    private void SetupHttpResponse<T>(T content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(content)
            });
    }

    private void SetupHttpFailure(HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }

    private WeatherApiForecastResponse CreateForecastResponse(
        DateOnly? date = null,
        double? avgTemp = null,
        double? maxTemp = null,
        double? minTemp = null,
        List<WeatherApiHour>? hours = null)
    {
        var d = date ?? _date;
        return new WeatherApiForecastResponse(
            new WeatherApiForecast(
            [
                new WeatherApiForecastDay(
                    d.ToString("yyyy-MM-dd"),
                    new WeatherApiDay(
                        avgTemp ?? _fixture.Create<double>(),
                        maxTemp ?? _fixture.Create<double>(),
                        minTemp ?? _fixture.Create<double>(),
                        _fixture.Create<double>(),
                        _fixture.Create<double>(),
                        _fixture.Create<int>(),
                        _fixture.Create<int>(),
                        new WeatherApiCondition(_fixture.Create<string>())),
                    hours ??
                    [
                        new WeatherApiHour(_fixture.Create<double>()), new WeatherApiHour(_fixture.Create<double>()),
                        new WeatherApiHour(_fixture.Create<double>())
                    ])
            ]));
    }

    [Fact]
    public async Task GetForecastAsync_SuccessfulResponse_ReturnsMappedForecast()
    {
        // Arrange
        var response = CreateForecastResponse();
        SetupHttpResponse(response);

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        var expected = response.Forecast.ForecastDay[0];
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Source.ShouldBe("WeatherAPI"),
            () => result.Forecast.MaxTempC.ShouldBe(expected.Day.MaxTempC),
            () => result.Forecast.MinTempC.ShouldBe(expected.Day.MinTempC),
            () => result.Forecast.AvgTempC.ShouldBe(expected.Day.AvgTempC),
            () => result.Forecast.Condition.ShouldBe(expected.Day.Condition.Text),
            () => result.Forecast.Humidity.ShouldBe(expected.Day.AvgHumidity),
            () => result.Forecast.WindSpeedKmh.ShouldBe(expected.Day.MaxWindKph),
            () => result.Forecast.PrecipitationMm.ShouldBe(expected.Day.TotalPrecipMm),
            () => result.Forecast.PrecipitationChance.ShouldBe(expected.Day.DailyChanceOfRain));
    }

    [Fact]
    public async Task GetForecastAsync_WhenNoMatchingDate_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(CreateForecastResponse(_date.AddDays(1)));

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_WhenForecastDaysEmpty_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(new WeatherApiForecastResponse(new WeatherApiForecast([])));

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_WhenHttpFails_ReturnsNull()
    {
        // Arrange
        SetupHttpFailure();

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_ApiKeyIncludedInQueryString()
    {
        // Arrange
        SetupHttpResponse(CreateForecastResponse());

        // Act
        await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.Query.Contains($"key={_apiKey}")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetForecastAsync_SourceNameIsWeatherAPI()
    {
        // Arrange
        SetupHttpResponse(CreateForecastResponse());

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result!.Source.ShouldBe("WeatherAPI");
    }
}
