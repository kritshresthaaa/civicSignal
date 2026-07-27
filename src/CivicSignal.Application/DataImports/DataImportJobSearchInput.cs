namespace CivicSignal.Application.DataImports;

public sealed record DataImportJobSearchInput(
    string? Source,
    string? Status,
    int Page,
    int PageSize);
