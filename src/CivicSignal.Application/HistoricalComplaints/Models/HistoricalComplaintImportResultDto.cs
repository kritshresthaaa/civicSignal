namespace CivicSignal.Application.HistoricalComplaints.Models;

public sealed record HistoricalComplaintImportResultDto(
    DateTimeOffset ImportedAt,
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount);
