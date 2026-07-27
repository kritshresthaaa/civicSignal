namespace CivicSignal.Application.Incidents.Models;

public sealed record ProcessingStepDto(
    Guid Id,
    string Name,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);
