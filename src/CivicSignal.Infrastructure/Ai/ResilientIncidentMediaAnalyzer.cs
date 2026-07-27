using CivicSignal.Application.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class ResilientIncidentMediaAnalyzer(
    AiServiceIncidentMediaAnalyzer primary,
    HeuristicIncidentMediaAnalyzer fallback,
    ILogger<ResilientIncidentMediaAnalyzer> logger) : IIncidentMediaAnalyzer
{
    public async Task<IncidentMediaAnalysisResult> AnalyzeAsync(
        IncidentMediaAnalysisRequest request,
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
                "AI service media analysis failed for incident {IncidentId}, media {MediaId}; using heuristic fallback.",
                request.IncidentId,
                request.Media.Id);

            return await fallback.AnalyzeAsync(request, cancellationToken);
        }
    }
}
