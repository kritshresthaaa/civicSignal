using CivicSignal.Domain.Incidents;

namespace CivicSignal.Application.Abstractions.Persistence;

public interface IIncidentRepository : IGenericRepository<Incident>
{
    Task<Incident?> GetByPublicTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken);

    Task<bool> PublicTrackingCodeExistsAsync(string trackingCode, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Incident>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken);

    void SetTextEmbedding(Incident incident, float[] embedding);
}
