namespace CivicSignal.Application.HistoricalComplaints.Models;

public sealed record HistoricalComplaintBucketDto(string Value, int Count);

public sealed record HistoricalComplaintSummaryDto(
    int TotalCount,
    DateTimeOffset? OldestCreatedAt,
    DateTimeOffset? NewestCreatedAt,
    IReadOnlyCollection<HistoricalComplaintBucketDto> TopCategories,
    IReadOnlyCollection<HistoricalComplaintBucketDto> TopAgencies,
    IReadOnlyCollection<HistoricalComplaintBucketDto> TopBoroughs);
