namespace CivicSignal.Application.HistoricalComplaints.Models;

public sealed record HistoricalComplaintDto(
    Guid Id,
    string Source,
    string ExternalId,
    string Category,
    string ComplaintType,
    string? Descriptor,
    string? Agency,
    string? AgencyName,
    string? Status,
    string? Borough,
    string? IncidentAddress,
    string? ResolutionDescription,
    double Latitude,
    double Longitude,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset ImportedAt,
    DateTimeOffset UpdatedAt);
