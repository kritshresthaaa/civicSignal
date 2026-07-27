namespace CivicSignal.Application.Abstractions.Persistence;

public sealed record HistoricalComplaintBucket(string Value, int Count);

public sealed record HistoricalComplaintSummaryResult(
    int TotalCount,
    DateTimeOffset? OldestCreatedAt,
    DateTimeOffset? NewestCreatedAt,
    IReadOnlyCollection<HistoricalComplaintBucket> TopCategories,
    IReadOnlyCollection<HistoricalComplaintBucket> TopAgencies,
    IReadOnlyCollection<HistoricalComplaintBucket> TopBoroughs);
