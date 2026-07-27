namespace CivicSignal.Application.AiEvaluations.Models;

public sealed record AiModelRunDto(
    string Name,
    string Provider,
    string ModelVersion,
    string Status,
    DateTimeOffset? EvaluatedAt,
    string Notes);
