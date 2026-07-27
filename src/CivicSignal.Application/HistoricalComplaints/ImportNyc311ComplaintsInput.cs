namespace CivicSignal.Application.HistoricalComplaints;

public sealed record ImportNyc311ComplaintsInput(
    int? Limit,
    int? DaysBack,
    string? ComplaintType,
    string? Borough);
