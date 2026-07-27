using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Incidents;

public sealed class IncidentReviewRecord
{
    private IncidentReviewRecord()
    {
    }

    private IncidentReviewRecord(
        Guid incidentId,
        ReviewDecision decision,
        Guid reviewerUserId,
        string? note,
        string? correctedCategory,
        string? correctedAgencyCode,
        IncidentSeverity? correctedSeverity,
        Guid? duplicateOfIncidentId,
        bool? acceptedPrediction,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Decision = decision;
        ReviewerUserId = reviewerUserId;
        Note = NormalizeOptional(note, 2_000);
        CorrectedCategory = NormalizeCategory(correctedCategory);
        CorrectedAgencyCode = NormalizeAgencyCode(correctedAgencyCode);
        CorrectedSeverity = correctedSeverity;
        DuplicateOfIncidentId = duplicateOfIncidentId;
        AcceptedPrediction = acceptedPrediction;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public ReviewDecision Decision { get; private set; }

    public Guid ReviewerUserId { get; private set; }

    public string? Note { get; private set; }

    public string? CorrectedCategory { get; private set; }

    public string? CorrectedAgencyCode { get; private set; }

    public IncidentSeverity? CorrectedSeverity { get; private set; }

    public Guid? DuplicateOfIncidentId { get; private set; }

    public bool? AcceptedPrediction { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static IncidentReviewRecord Create(
        Guid incidentId,
        ReviewDecision decision,
        Guid reviewerUserId,
        string? note,
        string? correctedCategory,
        string? correctedAgencyCode,
        IncidentSeverity? correctedSeverity,
        Guid? duplicateOfIncidentId,
        bool? acceptedPrediction,
        DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        if (!Enum.IsDefined(typeof(ReviewDecision), decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Review decision is not supported.");
        }

        if (reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer user id is required.", nameof(reviewerUserId));
        }

        if (duplicateOfIncidentId == incidentId)
        {
            throw new ArgumentException("An incident cannot be marked as a duplicate of itself.", nameof(duplicateOfIncidentId));
        }

        if (duplicateOfIncidentId == Guid.Empty)
        {
            throw new ArgumentException("Duplicate incident id cannot be empty.", nameof(duplicateOfIncidentId));
        }

        return new IncidentReviewRecord(
            incidentId,
            decision,
            reviewerUserId,
            note,
            correctedCategory,
            correctedAgencyCode,
            correctedSeverity,
            duplicateOfIncidentId,
            acceptedPrediction,
            createdAt);
    }

    private static string? NormalizeCategory(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new IncidentCategory(value).Value;
    }

    private static string? NormalizeAgencyCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new AgencyCode(value).Value;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
