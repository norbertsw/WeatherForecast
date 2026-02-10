using System.Text.Json.Serialization;

namespace WeatherForecast.Api.Clients.WeatherApi;

internal sealed record WeatherApiForecastResponse(WeatherApiForecast Forecast);

internal sealed record WeatherApiForecast(List<WeatherApiForecastDay> ForecastDay);

internal sealed record WeatherApiForecastDay(
    string Date,
    WeatherApiDay Day,
    List<WeatherApiHour> Hour);

internal sealed record WeatherApiDay(
    [property: JsonPropertyName("avgtemp_c")]
    double AvgTempC,
    [property: JsonPropertyName("maxtemp_c")]
    double MaxTempC,
    [property: JsonPropertyName("mintemp_c")]
    double MinTempC,
    [property: JsonPropertyName("maxwind_kph")]
    double MaxWindKph,
    [property: JsonPropertyName("totalprecip_mm")]
    double TotalPrecipMm,
    [property: JsonPropertyName("avghumidity")]
    int AvgHumidity,
    [property: JsonPropertyName("daily_chance_of_rain")]
    int DailyChanceOfRain,
    WeatherApiCondition Condition);

internal sealed record WeatherApiCondition(string Text);

internal sealed record WeatherApiHour(
    [property: JsonPropertyName("feelslike_c")]
    double FeelsLikeC);