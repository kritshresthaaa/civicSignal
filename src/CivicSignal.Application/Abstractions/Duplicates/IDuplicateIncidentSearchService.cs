using CivicSignal.Application.Abstractions.Ai;

namespace CivicSignal.Application.Abstractions.Duplicates;

public interface IDuplicateIncidentSearchService
{
    Task<IReadOnlyCollection<DuplicateIncidentCandidateResult>> FindDuplicatesAsync(
        IncidentAnalysisRequest request,
        IncidentAnalysisResult analysis,
        CancellationToken cancellationToken = default);
}
