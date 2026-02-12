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
using WeatherForecast.Api.Clients.VisualCrossing;
using Xunit;

namespace WeatherForecast.Api.Tests.Clients.VisualCrossing;

public class VisualCrossingClientTests
{
    private readonly IFixture _fixture;
    private readonly Mock<HttpMessageHandler> _handler;
    private readonly VisualCrossingClient _sut;
    private readonly DateOnly _date;

    public VisualCrossingClientTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _handler = new Mock<HttpMessageHandler>();
        var apiKey = _fixture.Create<string>();

        var options = Options.Create(new VisualCrossingOptions
        {
            ApiKey = apiKey,
            BaseUrl = "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/"
        });
        var httpClient = new HttpClient(_handler.Object)
        {
            BaseAddress = new Uri("https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/")
        };
        _sut = new VisualCrossingClient(httpClient, options, NullLogger<VisualCrossingClient>.Instance);
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

    private VisualCrossingResponse CreateResponse(
        double? temp = null, double? tempMax = null, double? tempMin = null,
        double? humidity = null, double? windSpeed = null, double? precip = null,
        string? conditions = null)
    {
        return new VisualCrossingResponse(
        [
            new VisualCrossingDay(
                temp ?? _fixture.Create<double>(),
                tempMax ?? _fixture.Create<double>(),
                tempMin ?? _fixture.Create<double>(),
                humidity ?? _fixture.Create<double>(),
                windSpeed ?? _fixture.Create<double>(),
                precip,
                conditions ?? _fixture.Create<string>())
        ]);
    }

    [Fact]
    public async Task GetForecastAsync_SuccessfulResponse_ReturnsMappedForecast()
    {
        // Arrange
        var response = CreateResponse();
        SetupHttpResponse(response);

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        var expected = response.Days[0];
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Source.ShouldBe("VisualCrossing"),
            () => result.Forecast.MaxTempC.ShouldBe(expected.Tempmax),
            () => result.Forecast.MinTempC.ShouldBe(expected.Tempmin),
            () => result.Forecast.AvgTempC.ShouldBe(expected.Temp),
            () => result.Forecast.Condition.ShouldBe(expected.Conditions),
            () => result.Forecast.WindSpeedKmh.ShouldBe(expected.Windspeed),
            () => result.Forecast.PrecipitationChance.ShouldBeNull(),
            () => result.Forecast.AvgFeelsLikeC.ShouldBeNull());
    }

    [Fact]
    public async Task GetForecastAsync_WhenDaysEmpty_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(new VisualCrossingResponse([]));

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
    public async Task GetForecastAsync_UsesFirstDayInResponse()
    {
        // Arrange
        var firstDay = new VisualCrossingDay(11.5, 15.0, 8.0, 75.0, 18.5, 2.5, "Cloudy");
        var secondDay = new VisualCrossingDay(
            _fixture.Create<double>(), _fixture.Create<double>(), _fixture.Create<double>(),
            _fixture.Create<double>(), _fixture.Create<double>(), _fixture.Create<double>(),
            _fixture.Create<string>());
        SetupHttpResponse(new VisualCrossingResponse([firstDay, secondDay]));

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Forecast.ShouldSatisfyAllConditions(
            () => result.Forecast.MinTempC.ShouldBe(firstDay.Tempmin),
            () => result.Forecast.AvgTempC.ShouldBe(firstDay.Temp),
            () => result.Forecast.MaxTempC.ShouldBe(firstDay.Tempmax),
            () => result.Forecast.Condition.ShouldBe(firstDay.Conditions));
    }

    [Fact]
    public async Task GetForecastAsync_DateFormattedInUrl()
    {
        // Arrange
        SetupHttpResponse(CreateResponse());

        // Act
        await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains($"/{_date:yyyy-MM-dd}?")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetForecastAsync_SourceNameIsVisualCrossing()
    {
        // Arrange
        SetupHttpResponse(CreateResponse());

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result!.Source.ShouldBe("VisualCrossing");
    }
}
