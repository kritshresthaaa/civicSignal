using CivicSignal.Application.AiEvaluations.Models;

namespace CivicSignal.Application.AiEvaluations;

public interface IAiEvaluationService
{
    Task<AiEvaluationBaselineReportDto> GetBaselineReportAsync(CancellationToken cancellationToken = default);
}
