using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using WeatherForecast.Api.Clients;
using WeatherForecast.Api.Weather.Forecast;
using Xunit;

namespace WeatherForecast.Api.Tests.Weather.Forecast;

public class ForecastHandlerTests
{
    private readonly IFixture _fixture;
    private readonly Mock<IDistributedCache> _cache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly DateOnly _date;

    public ForecastHandlerTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _cache = new Mock<IDistributedCache>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _logger = NullLogger.Instance;
        _date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
    }

    private WeatherForecastRequest CreateRequest(string? city = null, string? countryCode = null, DateOnly? date = null)
    {
        return new WeatherForecastRequest
        {
            City = city ?? _fixture.Create<string>(),
            CountryCode = countryCode ?? "GB",
            Date = date ?? _date
        };
    }

    private Mock<IWeatherClient> CreateMockClient(string? sourceName = null, ForecastSourceDto? result = null)
    {
        var client = new Mock<IWeatherClient>();
        client.Setup(c => c.SourceName).Returns(sourceName ?? _fixture.Create<string>());
        client.Setup(c => c.GetForecastAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return client;
    }

    private ForecastSourceDto CreateForecastSourceResponse(string? source = null)
    {
        return new ForecastSourceDto
        {
            Source = source ?? _fixture.Create<string>(),
            Forecast = _fixture.Create<ForecastDto>()
        };
    }

    private void SetupCacheHit(string cacheKey, WeatherForecastResponse response)
    {
        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        _cache.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheContainsData_ReturnsCachedResponse()
    {
        // Arrange
        var request = CreateRequest("London", "GB");
        var cacheKey = $"LONDON:GB:{request.Date:yyyy-MM-dd}";
        var cachedResponse = new WeatherForecastResponse
        {
            Date = request.Date,
            Location = new LocationDto { Name = "London", CountryCode = "GB" },
            Forecasts = [CreateForecastSourceResponse("AccuWeather")],
            Metadata = new MetadataDto { GeneratedAt = _timeProvider.GetUtcNow() }
        };
        SetupCacheHit(cacheKey, cachedResponse);
        var mockClient = CreateMockClient("AccuWeather");

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, [mockClient.Object], _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Location.Name.ShouldBe("London"),
            () => mockClient.Verify(c => c.GetForecastAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never));
    }

    [Fact]
    public async Task HandleAsync_WhenCacheIsEmpty_CallsAllWeatherClients()
    {
        // Arrange
        var request = CreateRequest();
        var clients = new[]
        {
            CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")),
            CreateMockClient("WeatherAPI", CreateForecastSourceResponse("WeatherAPI")),
            CreateMockClient("VisualCrossing", CreateForecastSourceResponse("VisualCrossing"))
        };

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Forecasts.Count.ShouldBe(3),
            () => clients[0].Verify(c => c.GetForecastAsync(request.City, request.CountryCode, request.Date, It.IsAny<CancellationToken>()), Times.Once),
            () => clients[1].Verify(c => c.GetForecastAsync(request.City, request.CountryCode, request.Date, It.IsAny<CancellationToken>()), Times.Once),
            () => clients[2].Verify(c => c.GetForecastAsync(request.City, request.CountryCode, request.Date, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task HandleAsync_WhenAllClientsReturnNull_ReturnsEmptyForecasts()
    {
        // Arrange
        var request = CreateRequest();
        var clients = new[]
        {
            CreateMockClient("AccuWeather"),
            CreateMockClient("WeatherAPI"),
            CreateMockClient("VisualCrossing")
        };

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Forecasts.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenSomeClientsReturnNull_ReturnsOnlySuccessfulForecasts()
    {
        // Arrange
        var request = CreateRequest();
        var clients = new[]
        {
            CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")),
            CreateMockClient("WeatherAPI"),
            CreateMockClient("VisualCrossing", CreateForecastSourceResponse("VisualCrossing"))
        };

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Forecasts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_CachesResultWithCorrectKey()
    {
        // Arrange
        var request = CreateRequest("London", "GB");
        var clients = new[] { CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")) };

        // Act
        await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        var expectedKey = $"LONDON:GB:{request.Date:yyyy-MM-dd}";
        _cache.Verify(c => c.SetAsync(
            expectedKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CacheKeyNormalizesCityToUpperCase()
    {
        // Arrange
        var request = CreateRequest("london", "GB");
        var clients = new[] { CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")) };

        // Act
        await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        _cache.Verify(c => c.SetAsync(
            It.Is<string>(k => k.StartsWith("LONDON:")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheReadFails_ContinuesWithApiCalls()
    {
        // Arrange
        var request = CreateRequest();
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache unavailable"));
        var mockClient = CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather"));
        var logger = new Mock<ILogger>();

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, [mockClient.Object], _timeProvider, logger.Object, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Forecasts.Count.ShouldBe(1),
            () => mockClient.Verify(c => c.GetForecastAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task HandleAsync_WhenCacheWriteFails_ReturnsResponseAnyway()
    {
        // Arrange
        var request = CreateRequest();
        _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache write failed"));
        var clients = new[] { CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")) };
        var logger = new Mock<ILogger>();

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, logger.Object, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            () => result.Forecasts.Count.ShouldBe(1),
            () => result.Forecasts.ShouldContain(f => f.Source == "AccuWeather"));
    }

    [Fact]
    public async Task HandleAsync_SetsGeneratedAtFromTimeProvider()
    {
        // Arrange
        var request = CreateRequest();
        var clients = new[] { CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")) };

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Metadata.GeneratedAt.ShouldBe(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task HandleAsync_SetsLocationFromRequest()
    {
        // Arrange
        var city = _fixture.Create<string>();
        var countryCode = "FR";
        var request = CreateRequest(city, countryCode);
        var clients = new[] { CreateMockClient("AccuWeather", CreateForecastSourceResponse("AccuWeather")) };

        // Act
        var result = await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Location.ShouldSatisfyAllConditions(
            () => result.Location.Name.ShouldBe(city),
            () => result.Location.CountryCode.ShouldBe(countryCode));
    }

    [Fact]
    public async Task HandleAsync_DoesNotCacheWhenNoResults()
    {
        // Arrange
        var request = CreateRequest();
        var clients = new[]
        {
            CreateMockClient("AccuWeather"),
            CreateMockClient("WeatherAPI"),
            CreateMockClient("VisualCrossing")
        };

        // Act
        await ForecastHandler.HandleAsync(
            request, _cache.Object, clients.Select(c => c.Object), _timeProvider, _logger, TestContext.Current.CancellationToken);

        // Assert
        _cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
