namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiEvaluationBaselineReportDto(
    string BaselineName,
    string ReportVersion,
    DateTimeOffset GeneratedAt,
    string Summary,
    IReadOnlyCollection<AiEvaluationFixtureCountDto> FixtureCounts,
    IReadOnlyCollection<AiEvaluationMetricGroupDto> MetricGroups,
    IReadOnlyCollection<AiEvaluationGateDto> Gates,
    IReadOnlyCollection<AiModelRunDto> ModelRuns,
    IReadOnlyCollection<AiEvaluationComparisonDto> Comparisons,
    IReadOnlyCollection<string> NextUpgrades);
