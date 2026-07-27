namespace CivicSignal.Api.Contracts.Incidents;

public sealed record ReviewIncidentRequest(
    string Decision,
    string? Note,
    string? CorrectedCategory = null,
    string? CorrectedAgencyCode = null,
    string? CorrectedSeverity = null,
    Guid? DuplicateOfIncidentId = null,
    bool? AcceptedPrediction = null);
