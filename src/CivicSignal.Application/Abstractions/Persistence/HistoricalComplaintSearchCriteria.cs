namespace CivicSignal.Application.Abstractions.Persistence;

public sealed record HistoricalComplaintSearchCriteria(
    string? Query,
    string? Category,
    string? ComplaintType,
    string? Agency,
    string? Status,
    string? Borough,
    double? Latitude,
    double? Longitude,
    double? RadiusMeters,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    int Page,
    int PageSize);
