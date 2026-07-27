namespace CivicSignal.Application.Abstractions.Duplicates;

public sealed record DuplicateIncidentCandidateResult(
    Guid CandidateIncidentId,
    double SimilarityScore,
    string? Reason);
