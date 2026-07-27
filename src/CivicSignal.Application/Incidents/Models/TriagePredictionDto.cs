namespace CivicSignal.Application.Incidents.Models;

public sealed record TriagePredictionDto(
    Guid Id,
    Guid IncidentId,
    string Category,
    string Severity,
    double Confidence,
    string Summary,
    string SuggestedAgencyCode,
    string ModelName,
    string? ModelVersion,
    string? PromptVersion,
    long? ProcessingTimeMilliseconds,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<PredictionEvidenceDto> Evidence);
