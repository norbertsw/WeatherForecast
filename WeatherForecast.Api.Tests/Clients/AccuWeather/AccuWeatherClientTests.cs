using System.Net;
using System.Net.Http.Json;
using System.Text;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shouldly;
using WeatherForecast.Api.Clients.AccuWeather;
using Xunit;

namespace WeatherForecast.Api.Tests.Clients.AccuWeather;

public class AccuWeatherClientTests
{
    private readonly IFixture _fixture;
    private readonly Mock<HttpMessageHandler> _handler;
    private readonly Mock<IDistributedCache> _cache;
    private readonly AccuWeatherClient _sut;
    private readonly DateOnly _date;

    public AccuWeatherClientTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _handler = new Mock<HttpMessageHandler>();
        _cache = new Mock<IDistributedCache>();

        var httpClient = new HttpClient(_handler.Object)
        {
            BaseAddress = new Uri("https://dataservice.accuweather.com/")
        };
        _sut = new AccuWeatherClient(httpClient, _cache.Object, NullLogger<AccuWeatherClient>.Instance);
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

    private void SetupHttpResponseSequence(params object[] responses)
    {
        var setup = _handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach (var response in responses)
            setup = setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
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

    private AccuWeatherForecastResponse CreateForecastResponse(
        DateOnly? date = null,
        double? minTemp = null, double? maxTemp = null,
        double? realFeelMin = null, double? realFeelMax = null)
    {
        var d = date ?? _date;
        return new AccuWeatherForecastResponse(
        [
            new AccuWeatherDailyForecast(
                $"{d:yyyy-MM-dd}T07:00:00+00:00",
                new AccuWeatherTemperatureRange(
                    new AccuWeatherMeasurement(minTemp ?? _fixture.Create<double>()),
                    new AccuWeatherMeasurement(maxTemp ?? _fixture.Create<double>())),
                new AccuWeatherTemperatureRange(
                    new AccuWeatherMeasurement(realFeelMin ?? _fixture.Create<double>()),
                    new AccuWeatherMeasurement(realFeelMax ?? _fixture.Create<double>())),
                new AccuWeatherDayNight(
                    new AccuWeatherWind(new AccuWeatherWindSpeed(_fixture.Create<double>())),
                    new AccuWeatherPrecipitation(_fixture.Create<double>()),
                    _fixture.Create<string>(),
                    new AccuWeatherRelativeHumidity(_fixture.Create<int>()),
                    _fixture.Create<int>()))
        ]);
    }

    private void SetupLocationCacheHit(string city, string countryCode, string locationKey)
    {
        var cacheKey = $"accu-loc:{city.ToUpperInvariant()}:{countryCode}";
        _cache.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(locationKey));
    }

    private void SetupCacheMiss()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
    }

    [Fact]
    public async Task GetForecastAsync_SuccessfulResponse_ReturnsMappedForecast()
    {
        // Arrange
        var locationKey = _fixture.Create<string>();
        SetupLocationCacheHit("London", "GB", locationKey);
        var response = CreateForecastResponse();
        SetupHttpResponse(response);

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        var expected = response.DailyForecasts[0];
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Source.ShouldBe("AccuWeather"),
            () => result.Forecast.MaxTempC.ShouldBe(expected.Temperature.Maximum.Value),
            () => result.Forecast.MinTempC.ShouldBe(expected.Temperature.Minimum.Value),
            () => result.Forecast.Condition.ShouldBe(expected.Day!.IconPhrase),
            () => result.Forecast.Humidity.ShouldBe(expected.Day!.RelativeHumidity.Average),
            () => result.Forecast.WindSpeedKmh.ShouldBe(expected.Day!.Wind!.Speed.Value),
            () => result.Forecast.PrecipitationMm.ShouldBe(expected.Day!.TotalLiquid!.Value),
            () => result.Forecast.PrecipitationChance.ShouldBe(expected.Day!.PrecipitationProbability));
    }

    [Fact]
    public async Task GetForecastAsync_AvgTempCalculation_IsMinPlusMaxDividedByTwo()
    {
        // Arrange
        SetupLocationCacheHit("London", "GB", _fixture.Create<string>());
        var response = CreateForecastResponse(minTemp: 10.0, maxTemp: 20.0);
        SetupHttpResponse(response);

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result!.Forecast.AvgTempC.ShouldBe(15.0);
    }

    [Fact]
    public async Task GetForecastAsync_AvgFeelsLikeCalculation_IsMinPlusMaxDividedByTwo()
    {
        // Arrange
        SetupLocationCacheHit("London", "GB", _fixture.Create<string>());
        var response = CreateForecastResponse(realFeelMin: 8.0, realFeelMax: 18.0);
        SetupHttpResponse(response);

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Forecast.AvgFeelsLikeC.ShouldBe(13.0);
    }

    [Fact]
    public async Task GetForecastAsync_WhenLocationCached_SkipsLocationApiCall()
    {
        // Arrange
        SetupLocationCacheHit("London", "GB", _fixture.Create<string>());
        SetupHttpResponse(CreateForecastResponse());

        // Act
        await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.AbsolutePath.Contains("forecasts")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetForecastAsync_WhenLocationNotCached_CachesLocationKey()
    {
        // Arrange
        var locationKey = _fixture.Create<string>();
        SetupCacheMiss();
        SetupHttpResponseSequence(
            new AccuWeatherLocation[] { new(locationKey) },
            CreateForecastResponse());

        // Act
        await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        _cache.Verify(c => c.SetAsync(
            "accu-loc:LONDON:GB",
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == locationKey),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForecastAsync_LocationKeyNormalizesCity()
    {
        // Arrange
        SetupCacheMiss();
        SetupHttpResponseSequence(
            new AccuWeatherLocation[] { new(_fixture.Create<string>()) },
            CreateForecastResponse());

        // Act
        await _sut.GetForecastAsync("london", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        _cache.Verify(c => c.GetAsync("accu-loc:LONDON:GB", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForecastAsync_WhenLocationSearchReturnsEmpty_ReturnsNull()
    {
        // Arrange
        SetupCacheMiss();
        SetupHttpResponse(Array.Empty<AccuWeatherLocation>());

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_WhenNoMatchingDate_ReturnsNull()
    {
        // Arrange
        SetupLocationCacheHit("London", "GB", _fixture.Create<string>());
        SetupHttpResponse(CreateForecastResponse(_date.AddDays(1)));

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_WhenHttpCallFails_ReturnsNull()
    {
        // Arrange
        SetupCacheMiss();
        SetupHttpFailure();

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetForecastAsync_WhenExceptionThrown_ReturnsNull()
    {
        // Arrange
        SetupCacheMiss();
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _sut.GetForecastAsync("London", "GB", _date, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }
}
