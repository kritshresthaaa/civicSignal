namespace CivicSignal.Application.Abstractions.Ai;

public interface IAiIncidentAnalyzer
{
    Task<IncidentAnalysisResult> AnalyzeAsync(
        IncidentAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
