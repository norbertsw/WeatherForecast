using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using WeatherForecast.Api.Weather.Forecast;
using Xunit;

namespace WeatherForecast.Api.Tests.Weather.Forecast;

public class ForecastRequestValidatorTests
{
    private readonly IFixture _fixture;
    private readonly ForecastRequestValidator _sut;
    private readonly DateOnly _today;

    public ForecastRequestValidatorTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _today = DateOnly.FromDateTime(DateTime.UtcNow);
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(_today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        _sut = new ForecastRequestValidator(timeProvider);
    }

    private WeatherForecastRequest CreateValidRequest(DateOnly? date = null)
    {
        return new WeatherForecastRequest
        {
            City = _fixture.Create<string>(),
            CountryCode = "GB",
            Date = date ?? _today
        };
    }

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_EmptyCity_IsInvalid()
    {
        // Arrange
        var request = new WeatherForecastRequest
        {
            City = "",
            CountryCode = "GB",
            Date = _today
        };

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "City"));
    }

    [Fact]
    public async Task Validate_EmptyCountryCode_IsInvalid()
    {
        // Arrange
        var request = new WeatherForecastRequest
        {
            City = _fixture.Create<string>(),
            CountryCode = "  ",
            Date = _today
        };

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "CountryCode"));
    }

    [Fact]
    public async Task Validate_CountryCodeTooShort_IsInvalid()
    {
        // Arrange
        var request = new WeatherForecastRequest
        {
            City = _fixture.Create<string>(),
            CountryCode = "G",
            Date = _today
        };

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "CountryCode"));
    }

    [Fact]
    public async Task Validate_CountryCodeTooLong_IsInvalid()
    {
        // Arrange
        var request = new WeatherForecastRequest
        {
            City = _fixture.Create<string>(),
            CountryCode = "GBR",
            Date = _today
        };

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "CountryCode"));
    }

    [Fact]
    public async Task Validate_DateInThePast_IsInvalid()
    {
        // Arrange
        var request = CreateValidRequest(_today.AddDays(-1));

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "Date"));
    }

    [Fact]
    public async Task Validate_DateMoreThan5DaysFromNow_IsInvalid()
    {
        // Arrange
        var request = CreateValidRequest(_today.AddDays(6));

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsValid.ShouldBeFalse(),
            () => result.Errors.ShouldContain(e => e.PropertyName == "Date"));
    }

    [Fact]
    public async Task Validate_DateExactly5DaysFromNow_IsValid()
    {
        // Arrange
        var request = CreateValidRequest(_today.AddDays(5));

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_DateIsToday_IsValid()
    {
        // Arrange
        var request = CreateValidRequest(_today);

        // Act
        var result = await _sut.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CountryCode_AutoUppercases_WhenSetToLowercase()
    {
        // Arrange & Act
        var request = new WeatherForecastRequest
        {
            City = _fixture.Create<string>(),
            CountryCode = "gb",
            Date = _today
        };

        // Assert
        request.CountryCode.ShouldBe("GB");
    }
}
