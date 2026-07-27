namespace CivicSignal.Api.Contracts.DataImports;

public sealed record CreateNyc311ImportJobRequest(
    int? Limit,
    int? DaysBack,
    string? ComplaintType,
    string? Borough);
