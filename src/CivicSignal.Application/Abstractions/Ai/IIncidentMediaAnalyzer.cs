namespace CivicSignal.Application.Abstractions.Ai;

public interface IIncidentMediaAnalyzer
{
    Task<IncidentMediaAnalysisResult> AnalyzeAsync(
        IncidentMediaAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
