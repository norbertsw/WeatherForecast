using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WeatherForecast.Api.Filters;
using Xunit;

namespace WeatherForecast.Api.Tests.Filters;

public class ApiKeyEndpointFilterTests
{
    private readonly IFixture _fixture;
    private readonly ApiKeyEndpointFilter _sut = new();
    private readonly string _validApiKey;

    public ApiKeyEndpointFilterTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _validApiKey = _fixture.Create<string>();
    }

    private DefaultHttpContext CreateHttpContext(string? apiKeyHeaderValue, string? configuredApiKey = null)
    {
        var httpContext = new DefaultHttpContext();

        if (apiKeyHeaderValue is not null)
            httpContext.Request.Headers["X-Api-Key"] = apiKeyHeaderValue;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = configuredApiKey ?? _validApiKey
            })
            .Build();

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        return httpContext;
    }

    private static EndpointFilterDelegate CreateNext(object? returnValue = null)
    {
        return _ => ValueTask.FromResult<object?>(returnValue ?? "next-called");
    }

    [Fact]
    public async Task InvokeAsync_WithValidApiKey_CallsNext()
    {
        // Arrange
        var httpContext = CreateHttpContext(_validApiKey);
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var expectedResult = _fixture.Create<string>();

        // Act
        var result = await _sut.InvokeAsync(context, CreateNext(expectedResult));

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingApiKeyHeader_Returns401()
    {
        // Arrange
        var httpContext = CreateHttpContext(null);
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await _sut.InvokeAsync(context, CreateNext());

        // Assert
        result.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var httpContext = CreateHttpContext(_fixture.Create<string>());
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await _sut.InvokeAsync(context, CreateNext());

        // Assert
        result.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task InvokeAsync_ApiKeyComparisonIsCaseSensitive()
    {
        // Arrange
        var httpContext = CreateHttpContext(_validApiKey.ToUpperInvariant(), _validApiKey.ToLowerInvariant());
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await _sut.InvokeAsync(context, CreateNext());

        // Assert
        result.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyApiKeyHeader_Returns401()
    {
        // Arrange
        var httpContext = CreateHttpContext("");
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await _sut.InvokeAsync(context, CreateNext());

        // Assert
        result.ShouldBeOfType<UnauthorizedHttpResult>();
    }
}
