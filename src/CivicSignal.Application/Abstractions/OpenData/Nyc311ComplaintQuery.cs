namespace CivicSignal.Application.Abstractions.OpenData;

public sealed record Nyc311ComplaintQuery(
    int Limit,
    int? DaysBack,
    string? ComplaintType,
    string? Borough);
