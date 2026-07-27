namespace CivicSignal.Application.Incidents;

public sealed record AssignIncidentInput(
    string AssignedTeam,
    Guid AssignedByUserId,
    string? AssignedAgencyCode = null,
    string? Note = null);
