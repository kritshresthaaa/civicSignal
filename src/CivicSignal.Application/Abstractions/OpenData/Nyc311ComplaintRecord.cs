namespace CivicSignal.Application.Abstractions.OpenData;

public sealed record Nyc311ComplaintRecord(
    string ExternalId,
    string ComplaintType,
    string? Descriptor,
    string? Agency,
    string? AgencyName,
    string? Status,
    string? Borough,
    string? IncidentAddress,
    string? ResolutionDescription,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ClosedAt);
