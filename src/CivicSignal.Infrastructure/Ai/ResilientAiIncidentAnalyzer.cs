using CivicSignal.Application.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class ResilientAiIncidentAnalyzer(
    AiServiceIncidentAnalyzer primary,
    HeuristicIncidentAnalyzer fallback,
    ILogger<ResilientAiIncidentAnalyzer> logger) : IAiIncidentAnalyzer
{
    public async Task<IncidentAnalysisResult> AnalyzeAsync(
        IncidentAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.AnalyzeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "AI service analysis failed for incident {IncidentId}; using heuristic fallback.",
                request.IncidentId);

            return await fallback.AnalyzeAsync(request, cancellationToken);
        }
    }
}
