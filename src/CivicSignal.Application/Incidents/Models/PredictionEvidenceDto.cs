namespace CivicSignal.Application.Incidents.Models;

public sealed record PredictionEvidenceDto(
    Guid Id,
    Guid TriagePredictionId,
    string Kind,
    string Title,
    string Detail,
    double? Confidence,
    DateTimeOffset CreatedAt);
