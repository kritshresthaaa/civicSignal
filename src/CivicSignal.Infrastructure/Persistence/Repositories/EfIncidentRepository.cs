using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace CivicSignal.Infrastructure.Persistence.Repositories;

internal sealed class EfIncidentRepository(CivicSignalDbContext dbContext)
    : EfGenericRepository<Incident>(dbContext), IIncidentRepository
{
    public new Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return IncludeIncidentDetails(DbContext.Incidents)
            .FirstOrDefaultAsync(incident => incident.Id == incidentId, cancellationToken);
    }

    public Task<Incident?> GetByPublicTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken)
    {
        var normalizedTrackingCode = Incident.NormalizePublicTrackingCode(trackingCode);

        return IncludeIncidentDetails(DbContext.Incidents)
            .FirstOrDefaultAsync(incident => incident.PublicTrackingCode == normalizedTrackingCode, cancellationToken);
    }

    public Task<bool> PublicTrackingCodeExistsAsync(string trackingCode, CancellationToken cancellationToken)
    {
        var normalizedTrackingCode = Incident.NormalizePublicTrackingCode(trackingCode);

        return DbContext.Incidents
            .AnyAsync(incident => incident.PublicTrackingCode == normalizedTrackingCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Incident>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = DbContext.Incidents;

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            if (!Enum.TryParse<IncidentStatus>(criteria.Status, ignoreCase: true, out var status))
            {
                return [];
            }

            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderByDescending(incident => incident.CreatedAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);
    }

    public void SetTextEmbedding(Incident incident, float[] embedding)
    {
        DbContext.Entry(incident)
            .Property("TextEmbedding")
            .CurrentValue = new Vector(embedding);
    }

    private static IQueryable<Incident> IncludeIncidentDetails(IQueryable<Incident> query)
    {
        return query
            .AsSplitQuery()
            .Include(incident => incident.ProcessingSteps)
            .Include(incident => incident.MediaItems)
            .Include(incident => incident.TriagePredictions)
                .ThenInclude(prediction => prediction.EvidenceItems)
            .Include(incident => incident.DuplicateCandidates)
            .Include(incident => incident.ReviewRecords)
            .Include(incident => incident.UpdateRequests)
            .Include(incident => incident.FeedbackItems);
    }
}
