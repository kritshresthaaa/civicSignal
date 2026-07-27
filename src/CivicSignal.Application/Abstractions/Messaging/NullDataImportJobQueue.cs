namespace CivicSignal.Application.Abstractions.Messaging;

public sealed class NullDataImportJobQueue : IDataImportJobQueue
{
    public Task EnqueueAsync(
        Guid jobId,
        string source,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
