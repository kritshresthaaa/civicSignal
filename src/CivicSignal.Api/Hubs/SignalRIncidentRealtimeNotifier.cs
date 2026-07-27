using CivicSignal.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace CivicSignal.Api.Hubs;

public sealed class SignalRIncidentRealtimeNotifier(
    IHubContext<IncidentStatusHub> hubContext,
    ILogger<SignalRIncidentRealtimeNotifier> logger) : IIncidentRealtimeNotifier
{
    public async Task PublishAsync(
        IncidentRealtimeEventDto incidentEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(
                hubContext.Clients
                    .Group(IncidentHubGroups.Incident(incidentEvent.IncidentId))
                    .SendAsync(IncidentHubEvents.IncidentUpdated, incidentEvent, cancellationToken),
                hubContext.Clients
                    .Group(IncidentHubGroups.Operations)
                    .SendAsync(IncidentHubEvents.OperationsIncidentUpdated, incidentEvent, cancellationToken));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Could not publish realtime update {EventType} for incident {IncidentId}.",
                incidentEvent.EventType,
                incidentEvent.IncidentId);
        }
    }
}
