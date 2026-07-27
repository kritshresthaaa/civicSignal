namespace CivicSignal.Application.Abstractions.Geocoding;

public interface IGeocodingService
{
    Task<IReadOnlyCollection<GeocodingResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<GeocodingResult?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
