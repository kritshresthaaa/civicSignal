using CivicSignal.Application.HistoricalComplaints.Models;

namespace CivicSignal.Application.HistoricalComplaints;

public interface IHistoricalComplaintService
{
    Task<IReadOnlyCollection<HistoricalComplaintDto>> SearchAsync(
        HistoricalComplaintSearchInput input,
        CancellationToken cancellationToken = default);

    Task<HistoricalComplaintSummaryDto> GetSummaryAsync(
        HistoricalComplaintSearchInput input,
        CancellationToken cancellationToken = default);

    Task<HistoricalComplaintImportResultDto> ImportNyc311Async(
        ImportNyc311ComplaintsInput input,
        CancellationToken cancellationToken = default);
}
