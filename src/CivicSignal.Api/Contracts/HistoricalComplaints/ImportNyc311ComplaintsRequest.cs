namespace CivicSignal.Api.Contracts.HistoricalComplaints;

public sealed record ImportNyc311ComplaintsRequest(
    int? Limit,
    int? DaysBack,
    string? ComplaintType,
    string? Borough);
