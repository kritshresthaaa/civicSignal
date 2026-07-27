namespace CivicSignal.Application.Abstractions.Geocoding;

public sealed record GeocodingResult(
    string DisplayName,
    double Latitude,
    double Longitude,
    string? Category,
    string? Type,
    double? Importance,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);
