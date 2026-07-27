namespace CivicSignal.Application.Abstractions.Realtime;

public interface IIncidentRealtimeNotifier
{
    Task PublishAsync(
        IncidentRealtimeEventDto incidentEvent,
        CancellationToken cancellationToken = default);
}
