namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentMediaDto(
    Guid Id,
    Guid IncidentId,
    string FileName,
    string ContentType,
    string StorageUri,
    string MediaType,
    string AnalysisStatus,
    string? AnalysisSummary,
    string? Transcript,
    IReadOnlyCollection<string> DetectedLabels,
    double? AnalysisConfidence,
    string? AnalysisModelName,
    string? AnalysisModelVersion,
    long? AnalysisProcessingTimeMilliseconds,
    string? AnalysisError,
    DateTimeOffset? AnalyzedAt,
    DateTimeOffset CreatedAt);
