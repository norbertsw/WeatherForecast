using Azure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using WeatherForecast.Api.Weather;
using WeatherForecast.Api.Weather.Forecast;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVaultUri"]!),
    new DefaultAzureCredential());

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

builder.Services.AddApplicationInsightsTelemetry();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddHealthChecks()
    .AddRedis(redisConnectionString!);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "WeatherForecast";
});

builder.Services.AddOpenApi(options => { options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>(); });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "v1"); });
}

app.UseRateLimiter();
app.UseHttpsRedirection();

app.UseExceptionHandler();

app.MapHealthChecks("/healthz");

app.MapWeatherApi();

app.Run();

internal sealed class ApiKeySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
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

        document.Security =
        [
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