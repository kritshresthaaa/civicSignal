namespace CivicSignal.Application.Incidents;

public sealed record DispatchIncidentInput(
    Guid DispatchedByUserId,
    string? Note = null);
