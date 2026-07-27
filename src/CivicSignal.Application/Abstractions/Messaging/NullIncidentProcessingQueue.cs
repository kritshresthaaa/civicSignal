namespace CivicSignal.Application.Abstractions.Messaging;

public sealed class NullIncidentProcessingQueue : IIncidentProcessingQueue
{
    public Task EnqueueAsync(
        Guid incidentId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
