using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using WeatherForecast.Api.Weather;
using WeatherForecast.Api.Weather.Forecast;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 15;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
builder.Services.AddScoped<IValidator<WeatherForecastRequest>, ForecastRequestValidator>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>();

});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}


app.UseRateLimiter();
app.UseHttpsRedirection();

app.UseExceptionHandler();

app.MapHealthChecks("/healthz");

app.MapWeatherApi();

app.Run();

internal sealed class ApiKeySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["ApiKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header
            }
        };

        document.Security = [
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("ApiKey"),
                    []
                }
            }
        ];

        document.SetReferenceHostDocument();

        return Task.CompletedTask;
    }
}

internal sealed class ApiKeyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey")] = []
        });
        return Task.CompletedTask;
    }
}