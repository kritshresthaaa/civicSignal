using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Geocoding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Geocoding;

internal sealed class NominatimGeocodingService(
    HttpClient httpClient,
    IApplicationCache cache,
    IOptions<NominatimOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    public async Task<IReadOnlyCollection<GeocodingResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 3)
        {
            return [];
        }

        var cacheKey = $"geocoding:nominatim:search:{normalizedQuery.ToLowerInvariant()}";
        var cached = await cache.GetAsync<GeocodingResult[]>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var results = await FetchSearchResultsAsync(normalizedQuery, cancellationToken);
        await cache.SetAsync(cacheKey, results, options.Value.CacheDuration, cancellationToken);

        return results;
    }

    public async Task<GeocodingResult?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidLatitude(latitude) || !IsValidLongitude(longitude))
        {
            return null;
        }

        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"geocoding:nominatim:reverse:{Math.Round(latitude, 5):0.00000}:{Math.Round(longitude, 5):0.00000}");
        var cached = await cache.GetAsync<GeocodingResult>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await FetchReverseResultAsync(latitude, longitude, cancellationToken);
        if (result is not null)
        {
            await cache.SetAsync(cacheKey, result, options.Value.CacheDuration, cancellationToken);
        }

        return result;
    }

    private async Task<IReadOnlyCollection<GeocodingResult>> FetchSearchResultsAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = BuildSearchEndpoint(query);
            var payload = await httpClient.GetFromJsonAsync<IReadOnlyCollection<NominatimPlace>>(
                endpoint,
                cancellationToken);

            return payload?
                .Select(MapPlace)
                .OfType<GeocodingResult>()
                .ToArray() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nominatim search failed for query {Query}.", query);
            return [];
        }
    }

    private async Task<GeocodingResult?> FetchReverseResultAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = string.Create(
                CultureInfo.InvariantCulture,
                $"/reverse?format=jsonv2&addressdetails=1&lat={latitude:0.#####}&lon={longitude:0.#####}&zoom=18");

            var payload = await httpClient.GetFromJsonAsync<NominatimPlace>(endpoint, cancellationToken);

            return payload is null ? null : MapPlace(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Nominatim reverse geocoding failed for {Latitude}, {Longitude}.",
                latitude,
                longitude);
            return null;
        }
    }

    private string BuildSearchEndpoint(string query)
    {
        var endpoint = $"/search?format=jsonv2&addressdetails=1&limit={options.Value.NormalizedSearchLimit}&q={Uri.EscapeDataString(query)}";
        var countryCodes = options.Value.CountryCodes.Trim();

        return string.IsNullOrWhiteSpace(countryCodes)
            ? endpoint
            : $"{endpoint}&countrycodes={Uri.EscapeDataString(countryCodes)}";
    }

    private static GeocodingResult? MapPlace(NominatimPlace place)
    {
        if (!TryParseCoordinate(place.Latitude, out var latitude)
            || !TryParseCoordinate(place.Longitude, out var longitude))
        {
            return null;
        }

        var address = place.Address;

        return new GeocodingResult(
            place.DisplayName ?? FormatCoordinates(latitude, longitude),
            latitude,
            longitude,
            place.Category,
            place.Type,
            place.Importance,
            BuildAddressLine(address),
            address?.City ?? address?.Town ?? address?.Village ?? address?.Hamlet ?? address?.Suburb ?? address?.County,
            address?.State,
            address?.PostalCode,
            address?.Country);
    }

    private static string? BuildAddressLine(NominatimAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var street = address.Road ?? address.Pedestrian ?? address.Footway ?? address.Path;
        if (string.IsNullOrWhiteSpace(street))
        {
            return address.Neighbourhood ?? address.Suburb;
        }

        return string.IsNullOrWhiteSpace(address.HouseNumber)
            ? street
            : $"{address.HouseNumber} {street}";
    }

    private static bool TryParseCoordinate(string? value, out double coordinate)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate);
    }

    private static bool IsValidLatitude(double latitude)
    {
        return double.IsFinite(latitude) && latitude is >= -90 and <= 90;
    }

    private static bool IsValidLongitude(double longitude)
    {
        return double.IsFinite(longitude) && longitude is >= -180 and <= 180;
    }

    private static string FormatCoordinates(double latitude, double longitude)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{latitude:0.#####}, {longitude:0.#####}");
    }

    private sealed class NominatimPlace
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("lat")]
        public string? Latitude { get; init; }

        [JsonPropertyName("lon")]
        public string? Longitude { get; init; }

        [JsonPropertyName("category")]
        public string? Category { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("importance")]
        public double? Importance { get; init; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; init; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; init; }

        [JsonPropertyName("road")]
        public string? Road { get; init; }

        [JsonPropertyName("pedestrian")]
        public string? Pedestrian { get; init; }

        [JsonPropertyName("footway")]
        public string? Footway { get; init; }

        [JsonPropertyName("path")]
        public string? Path { get; init; }

        [JsonPropertyName("neighbourhood")]
        public string? Neighbourhood { get; init; }

        [JsonPropertyName("suburb")]
        public string? Suburb { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("town")]
        public string? Town { get; init; }

        [JsonPropertyName("village")]
        public string? Village { get; init; }

        [JsonPropertyName("hamlet")]
        public string? Hamlet { get; init; }

        [JsonPropertyName("county")]
        public string? County { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("postcode")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }
    }
}
