namespace CivicSignal.Application.Incidents;

public sealed record ReviewIncidentInput(
    string Decision,
    string? Note,
    Guid ReviewerUserId,
    string? CorrectedCategory = null,
    string? CorrectedAgencyCode = null,
    string? CorrectedSeverity = null,
    Guid? DuplicateOfIncidentId = null,
    bool? AcceptedPrediction = null);
