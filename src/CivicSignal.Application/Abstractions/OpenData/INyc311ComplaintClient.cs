namespace CivicSignal.Application.Abstractions.OpenData;

public interface INyc311ComplaintClient
{
    Task<IReadOnlyCollection<Nyc311ComplaintRecord>> GetComplaintsAsync(
        Nyc311ComplaintQuery query,
        CancellationToken cancellationToken = default);
}
