namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiEvaluationMetricGroupDto(
    string Name,
    string Summary,
    IReadOnlyCollection<AiEvaluationMetricDto> Metrics);
