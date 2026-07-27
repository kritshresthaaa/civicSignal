namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentAnalysisResult(
    string Category,
    string Severity,
    double Confidence,
    string Summary,
    string SuggestedAgencyCode,
    string ModelName,
    string? ModelVersion = null,
    string? PromptVersion = null,
    long? ProcessingTimeMilliseconds = null,
    IReadOnlyCollection<IncidentAnalysisEvidence>? Evidence = null);
