namespace CivicSignal.Infrastructure.Duplicates;

internal sealed class DuplicateDetectionOptions
{
    public const string SectionName = "DuplicateDetection";

    public double SearchRadiusMeters { get; set; } = 500;

    public int TimeWindowHours { get; set; } = 168;

    public int CandidatePoolSize { get; set; } = 50;

    public int MaxResults { get; set; } = 5;

    public double MinimumScore { get; set; } = 0.7;

    public double TextWeight { get; set; } = 0.55;

    public double GeographyWeight { get; set; } = 0.3;

    public double TimeWeight { get; set; } = 0.15;

    public double NormalizedSearchRadiusMeters => Math.Clamp(SearchRadiusMeters, 25, 10_000);

    public double NormalizedTimeWindowHours => Math.Clamp(TimeWindowHours, 1, 24 * 90);

    public int NormalizedCandidatePoolSize => Math.Clamp(CandidatePoolSize, 1, 500);

    public int NormalizedMaxResults => Math.Clamp(MaxResults, 1, 50);

    public double NormalizedMinimumScore => Math.Clamp(MinimumScore, 0, 0.99);
}
