namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentAnalysisEvidence(
    string Kind,
    string Title,
    string Detail,
    double? Confidence = null);
