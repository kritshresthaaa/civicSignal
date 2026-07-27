namespace CivicSignal.Application.Abstractions.Persistence;

public sealed record DataImportJobSearchCriteria(
    string? Source,
    string? Status,
    int Page,
    int PageSize);
