namespace CivicSignal.Application.Incidents;

public sealed record IncidentSearchInput(
    string? Status,
    int Page = 1,
    int PageSize = 50);
