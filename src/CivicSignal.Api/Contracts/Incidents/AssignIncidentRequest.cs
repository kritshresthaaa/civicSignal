namespace CivicSignal.Api.Contracts.Incidents;

public sealed record AssignIncidentRequest(
    string AssignedTeam,
    string? AssignedAgencyCode = null,
    string? Note = null);
