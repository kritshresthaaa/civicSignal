using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Agents.Models;

namespace CivicSignal.Application.Agents;

internal static class WeatherContextMappings
{
    public static WeatherContextDto ToDto(this WeatherObservationResult result)
    {
        return new WeatherContextDto(
            result.IsAvailable,
            result.Provider,
            result.StationIdentifier,
            result.Summary,
            result.TemperatureCelsius,
            result.WindSpeedKph,
            result.WindDirection,
            result.PrecipitationLastHourMillimeters,
            result.SevereAlertSummary,
            result.UnavailableReason,
            result.RetrievedAt);
    }
}
