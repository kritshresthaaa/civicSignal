namespace CivicSignal.Application.DataImports;

public sealed record CreateNyc311ImportJobInput(
    int? Limit,
    int? DaysBack,
    string? ComplaintType,
    string? Borough,
    Guid? RequestedByUserId);
