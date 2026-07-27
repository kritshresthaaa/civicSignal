namespace CivicSignal.Api.Contracts.Incidents;

public sealed record LinkDuplicateIncidentRequest(
    Guid DuplicateOfIncidentId,
    string? Note = null);
