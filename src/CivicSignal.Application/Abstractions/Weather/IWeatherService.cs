namespace CivicSignal.Application.Abstractions.Weather;

public interface IWeatherService
{
    Task<WeatherObservationResult> GetCurrentConditionsAsync(
        double latitude,
        double longitude,
        DateTimeOffset observedNear,
        CancellationToken cancellationToken = default);
}
