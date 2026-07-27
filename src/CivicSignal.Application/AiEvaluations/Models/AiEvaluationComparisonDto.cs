namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiEvaluationComparisonDto(
    string Capability,
    string Baseline,
    string FutureTarget,
    string DecisionRule);
