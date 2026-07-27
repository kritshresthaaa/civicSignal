using CivicSignal.Domain.DataImports;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents;

namespace CivicSignal.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    IQueryable<Incident> Incidents { get; }

    IQueryable<DataImportJob> DataImportJobs { get; }

    IQueryable<HistoricalComplaint> HistoricalComplaints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
