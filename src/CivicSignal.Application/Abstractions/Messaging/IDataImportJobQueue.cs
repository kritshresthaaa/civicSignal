namespace CivicSignal.Application.Abstractions.Messaging;

public interface IDataImportJobQueue
{
    Task EnqueueAsync(
        Guid jobId,
        string source,
        CancellationToken cancellationToken = default);
}
