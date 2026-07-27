using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Incidents;

public sealed class DuplicateCandidate
{
    private DuplicateCandidate()
    {
    }

    private DuplicateCandidate(
        Guid incidentId,
        Guid candidateIncidentId,
        ConfidenceScore similarityScore,
        string? reason,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        CandidateIncidentId = candidateIncidentId;
        SimilarityScore = similarityScore;
        Reason = NormalizeReason(reason);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public Guid CandidateIncidentId { get; private set; }

    public ConfidenceScore SimilarityScore { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static DuplicateCandidate Create(
        Guid incidentId,
        Guid candidateIncidentId,
        ConfidenceScore similarityScore,
        string? reason,
        DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        if (candidateIncidentId == Guid.Empty)
        {
            throw new ArgumentException("Candidate incident id is required.", nameof(candidateIncidentId));
        }

        if (candidateIncidentId == incidentId)
        {
            throw new ArgumentException("An incident cannot be marked as a duplicate of itself.", nameof(candidateIncidentId));
        }

        return new DuplicateCandidate(incidentId, candidateIncidentId, similarityScore, reason, createdAt);
    }

    internal void Update(ConfidenceScore similarityScore, string? reason, DateTimeOffset updatedAt)
    {
        SimilarityScore = similarityScore;
        Reason = NormalizeReason(reason);
        UpdatedAt = updatedAt;
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = reason.Trim();
        if (normalized.Length > 1_000)
        {
            throw new ArgumentException("Reason cannot exceed 1000 characters.", nameof(reason));
        }

        return normalized;
    }
}
