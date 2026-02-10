namespace WeatherForecast.Api.Filters;

public class ApiKeyEndpointFilter : IEndpointFilter
{
    private const string ApiKeyHeader = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var extractedApiKey))
            return TypedResults.Unauthorized();

        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var apiKey = config.GetValue<string>("ApiKey");

        if (!string.Equals(apiKey, extractedApiKey, StringComparison.Ordinal))
            return TypedResults.Unauthorized();

        return await next(context);
    }
}