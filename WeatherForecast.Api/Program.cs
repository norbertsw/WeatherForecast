using System.Threading.RateLimiting;
using Asp.Versioning;
using Azure.Identity;
using FluentValidation;
using WeatherForecast.Api.Clients;
using WeatherForecast.Api.Weather;
using WeatherForecast.Api.Weather.Forecast;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri)) builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());

builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-api-key", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddScoped<IValidator<WeatherForecastRequest>, ForecastRequestValidator>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddWeatherClients(builder.Configuration);

var hc = builder.Services.AddHealthChecks();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "WeatherForecast";
    });

    hc.AddRedis(redisConnectionString!);
}

var app = builder.Build();

app.UseHttpsRedirection();
app.UsePathBase(new PathString("/api"));
app.UseRateLimiter();

app.UseExceptionHandler();

app.MapHealthChecks("/healthz");

app.MapWeatherApi();

app.Run();