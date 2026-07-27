namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentMediaAnalysisResult(
    string Summary,
    string? Transcript,
    IReadOnlyCollection<string> DetectedLabels,
    double? Confidence,
    string ModelName,
    string? ModelVersion = null,
    long? ProcessingTimeMilliseconds = null,
    IReadOnlyCollection<IncidentAnalysisEvidence>? Evidence = null);
