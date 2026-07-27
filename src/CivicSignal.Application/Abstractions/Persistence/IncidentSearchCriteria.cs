namespace CivicSignal.Application.Abstractions.Persistence;

public sealed record IncidentSearchCriteria(
    string? Status,
    int Page,
    int PageSize);
