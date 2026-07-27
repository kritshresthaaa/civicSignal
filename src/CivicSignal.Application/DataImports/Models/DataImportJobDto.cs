namespace CivicSignal.Application.DataImports.Models;

public sealed record DataImportJobDto(
    Guid Id,
    string Source,
    string ImportType,
    string ParametersJson,
    string Status,
    Guid? RequestedByUserId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);
