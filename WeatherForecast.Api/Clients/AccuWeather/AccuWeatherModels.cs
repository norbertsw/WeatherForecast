namespace WeatherForecast.Api.Clients.AccuWeather;

internal sealed record AccuWeatherLocation(string Key);

internal sealed record AccuWeatherForecastResponse(List<AccuWeatherDailyForecast> DailyForecasts);

internal sealed record AccuWeatherDailyForecast(
    string Date,
    AccuWeatherTemperatureRange Temperature,
    AccuWeatherTemperatureRange RealFeelTemperature,
    AccuWeatherDayNight? Day);

internal sealed record AccuWeatherTemperatureRange(
    AccuWeatherMeasurement Minimum,
    AccuWeatherMeasurement Maximum);

internal sealed record AccuWeatherMeasurement(double Value);

internal sealed record AccuWeatherDayNight(
    AccuWeatherWind? Wind,
    AccuWeatherPrecipitation? TotalLiquid,
    string IconPhrase,
    AccuWeatherRelativeHumidity RelativeHumidity,
    int? PrecipitationProbability);

internal sealed record AccuWeatherRelativeHumidity(int Average);

internal sealed record AccuWeatherWind(AccuWeatherWindSpeed Speed);

internal sealed record AccuWeatherWindSpeed(double Value);

internal sealed record AccuWeatherPrecipitation(double Value);