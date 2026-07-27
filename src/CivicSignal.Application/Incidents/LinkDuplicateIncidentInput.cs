namespace CivicSignal.Application.Incidents;

public sealed record LinkDuplicateIncidentInput(
    Guid DuplicateOfIncidentId,
    Guid LinkedByUserId,
    string? Note = null);
