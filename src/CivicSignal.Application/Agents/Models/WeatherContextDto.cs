namespace CivicSignal.Application.Agents.Models;

public sealed record WeatherContextDto(
    bool IsAvailable,
    string Provider,
    string? StationIdentifier,
    string? Summary,
    double? TemperatureCelsius,
    double? WindSpeedKph,
    string? WindDirection,
    double? PrecipitationLastHourMillimeters,
    string? SevereAlertSummary,
    string? UnavailableReason,
    DateTimeOffset RetrievedAt);
