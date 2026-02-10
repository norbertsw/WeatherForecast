using System.Text.Json.Serialization;

namespace WeatherForecast.Api.Clients.VisualCrossing;

internal sealed record VisualCrossingResponse(List<VisualCrossingDay> Days);

internal sealed record VisualCrossingDay(
    [property: JsonPropertyName("temp")] double Temp,
    [property: JsonPropertyName("tempmax")] double Tempmax,
    [property: JsonPropertyName("tempmin")] double Tempmin,
    [property: JsonPropertyName("humidity")] double Humidity,
    [property: JsonPropertyName("windspeed")] double Windspeed,
    [property: JsonPropertyName("precip")] double? Precip,
    [property: JsonPropertyName("conditions")] string Conditions);
