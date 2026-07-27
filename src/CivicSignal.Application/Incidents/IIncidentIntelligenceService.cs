using CivicSignal.Application.Incidents.Models;

namespace CivicSignal.Application.Incidents;

public interface IIncidentIntelligenceService
{
    Task<IncidentMediaDto> AddMediaAsync(
        Guid incidentId,
        AddIncidentMediaInput input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentMediaDto>?> GetMediaAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IncidentMediaDto> AnalyzeMediaAsync(
        Guid incidentId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<TriagePredictionDto> AnalyzeAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<TriagePredictionDto?> GetLatestPredictionAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DuplicateCandidateDto>?> GetDuplicateCandidatesAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);
}
