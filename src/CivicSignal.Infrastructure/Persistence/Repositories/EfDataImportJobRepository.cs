using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Domain.DataImports;
using Microsoft.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.Persistence.Repositories;

internal sealed class EfDataImportJobRepository(CivicSignalDbContext dbContext)
    : EfGenericRepository<DataImportJob>(dbContext), IDataImportJobRepository
{
    public async Task<IReadOnlyCollection<DataImportJob>> SearchAsync(
        DataImportJobSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        IQueryable<DataImportJob> query = DbContext.DataImportJobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.Source))
        {
            query = query.Where(job => job.Source == criteria.Source);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            if (!Enum.TryParse<DataImportJobStatus>(criteria.Status, ignoreCase: true, out var status))
            {
                return [];
            }

            query = query.Where(job => job.Status == status);
        }

        return await query
            .OrderByDescending(job => job.RequestedAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DataImportJob>> GetPendingAsync(
        int count,
        CancellationToken cancellationToken)
    {
        return await DbContext.DataImportJobs
            .Where(job => job.Status == DataImportJobStatus.Pending)
            .OrderBy(job => job.RequestedAt)
            .Take(count)
            .ToArrayAsync(cancellationToken);
    }
}
