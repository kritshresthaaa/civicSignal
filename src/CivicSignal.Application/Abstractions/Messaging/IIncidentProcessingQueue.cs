namespace CivicSignal.Application.Abstractions.Messaging;

public interface IIncidentProcessingQueue
{
    Task EnqueueAsync(
        Guid incidentId,
        string trigger,
        CancellationToken cancellationToken = default);
}
