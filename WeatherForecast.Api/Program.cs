using Asp.Versioning;
using Azure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using WeatherForecast.Api.Clients;
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
app.UseRateLimiter();

app.UseExceptionHandler();

app.MapHealthChecks("/healthz");

app.UsePathBase(new PathString("/api"));
app.MapWeatherApi();

app.Run();