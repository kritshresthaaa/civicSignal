namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiEvaluationMetricDto(
    string Name,
    double Value,
    string Unit,
    double? Threshold,
    bool IsHigherBetter,
    bool Passed);
