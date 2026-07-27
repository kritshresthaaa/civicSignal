namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentMediaDescriptor(
    Guid Id,
    string FileName,
    string ContentType,
    string StorageUri,
    string MediaType,
    string AnalysisStatus = "Pending",
    string? AnalysisSummary = null,
    string? Transcript = null,
    IReadOnlyCollection<string>? DetectedLabels = null);
