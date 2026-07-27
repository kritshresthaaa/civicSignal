namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentFeedbackDto(
    Guid Id,
    Guid IncidentId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);
