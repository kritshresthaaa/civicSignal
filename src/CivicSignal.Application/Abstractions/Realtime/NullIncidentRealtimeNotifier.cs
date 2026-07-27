namespace CivicSignal.Application.Abstractions.Realtime;

internal sealed class NullIncidentRealtimeNotifier : IIncidentRealtimeNotifier
{
    public Task PublishAsync(
        IncidentRealtimeEventDto incidentEvent,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
