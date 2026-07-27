namespace CivicSignal.Application.Abstractions.Geocoding;

public sealed class NullGeocodingService : IGeocodingService
{
    public Task<IReadOnlyCollection<GeocodingResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<GeocodingResult>>([]);
    }

    public Task<GeocodingResult?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<GeocodingResult?>(null);
    }
}
