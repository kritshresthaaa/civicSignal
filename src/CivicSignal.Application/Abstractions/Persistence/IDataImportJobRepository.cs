using CivicSignal.Domain.DataImports;

namespace CivicSignal.Application.Abstractions.Persistence;

public interface IDataImportJobRepository : IGenericRepository<DataImportJob>
{
    Task<IReadOnlyCollection<DataImportJob>> SearchAsync(
        DataImportJobSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DataImportJob>> GetPendingAsync(
        int count,
        CancellationToken cancellationToken);
}
