namespace CivicSignal.Application.Incidents.Models;

public sealed record DuplicateCandidateDto(
    Guid Id,
    Guid IncidentId,
    Guid CandidateIncidentId,
    double SimilarityScore,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
