using CivicSignal.Domain.HistoricalComplaints;

namespace CivicSignal.Application.Abstractions.Persistence;

public interface IHistoricalComplaintRepository : IGenericRepository<HistoricalComplaint>
{
    Task<HistoricalComplaint?> GetBySourceExternalIdAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<HistoricalComplaint>> SearchAsync(
        HistoricalComplaintSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<HistoricalComplaintSummaryResult> GetSummaryAsync(
        HistoricalComplaintSearchCriteria criteria,
        CancellationToken cancellationToken);
}
