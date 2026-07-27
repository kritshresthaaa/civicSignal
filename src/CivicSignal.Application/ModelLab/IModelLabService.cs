using CivicSignal.Application.ModelLab.Models;

namespace CivicSignal.Application.ModelLab;

public interface IModelLabService
{
    Task<ModelLabAnalysisDto> AnalyzeAsync(
        ModelLabAnalysisInput input,
        CancellationToken cancellationToken = default);
}
