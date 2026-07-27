namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentUpdateRequestDto(
    Guid Id,
    Guid IncidentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt);
