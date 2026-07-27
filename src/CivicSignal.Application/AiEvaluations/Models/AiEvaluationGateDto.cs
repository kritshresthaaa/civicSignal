namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiEvaluationGateDto(
    string Name,
    string Category,
    double Value,
    string Unit,
    double Threshold,
    bool IsHigherBetter,
    bool Passed,
    string Rationale);
