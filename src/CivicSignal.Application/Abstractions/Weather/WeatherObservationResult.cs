namespace CivicSignal.Application.Abstractions.Weather;

public sealed record WeatherObservationResult(
    bool IsAvailable,
    string Provider,
    DateTimeOffset RetrievedAt,
    string? StationIdentifier,
    string? Summary,
    double? TemperatureCelsius,
    double? WindSpeedKph,
    string? WindDirection,
    double? PrecipitationLastHourMillimeters,
    string? SevereAlertSummary,
    string? UnavailableReason)
{
    public static WeatherObservationResult Unavailable(
        string provider,
        string reason,
        DateTimeOffset retrievedAt)
    {
        return new WeatherObservationResult(
            false,
            provider,
            retrievedAt,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            reason);
    }
}
