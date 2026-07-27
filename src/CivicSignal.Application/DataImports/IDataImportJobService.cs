using CivicSignal.Application.DataImports.Models;

namespace CivicSignal.Application.DataImports;

public interface IDataImportJobService
{
    Task<DataImportJobDto> QueueNyc311ImportAsync(
        CreateNyc311ImportJobInput input,
        CancellationToken cancellationToken = default);

    Task<DataImportJobDto?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DataImportJobDto>> SearchAsync(
        DataImportJobSearchInput input,
        CancellationToken cancellationToken = default);

    Task<DataImportJobDto> RetryAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<int> RunPendingAsync(
        int count,
        CancellationToken cancellationToken = default);

    Task<DataImportJobDto> RunJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
