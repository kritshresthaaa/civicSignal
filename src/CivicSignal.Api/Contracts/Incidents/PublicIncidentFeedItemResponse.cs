namespace CivicSignal.Api.Contracts.Incidents;

public sealed record PublicIncidentFeedItemResponse(
    string TrackingCode,
    string Description,
    double ApproximateLatitude,
    double ApproximateLongitude,
    string Status,
    DateTimeOffset CreatedAt,
    string Category,
    string Severity,
    string? AgencyCode,
    bool HasReview,
    bool IsDuplicate,
    string AreaLabel,
    int MediaCount,
    string? LatestImageUrl,
    string? LatestMediaSummary);
