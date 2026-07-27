namespace CivicSignal.Application.Abstractions.Weather;

public sealed class NullWeatherService : IWeatherService
{
    public Task<WeatherObservationResult> GetCurrentConditionsAsync(
        double latitude,
        double longitude,
        DateTimeOffset observedNear,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WeatherObservationResult.Unavailable(
            "none",
            "Weather integration is disabled.",
            DateTimeOffset.UtcNow));
    }
}
