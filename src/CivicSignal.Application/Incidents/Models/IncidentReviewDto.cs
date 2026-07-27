namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentReviewDto(
    Guid Id,
    Guid IncidentId,
    string Decision,
    string? Note,
    Guid ReviewerUserId,
    string? CorrectedCategory,
    string? CorrectedAgencyCode,
    string? CorrectedSeverity,
    Guid? DuplicateOfIncidentId,
    bool? AcceptedPrediction,
    DateTimeOffset CreatedAt);
