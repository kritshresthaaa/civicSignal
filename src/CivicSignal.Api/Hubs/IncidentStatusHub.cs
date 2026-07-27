using CivicSignal.Application.Identity;
using CivicSignal.Application.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CivicSignal.Api.Hubs;

public sealed class IncidentStatusHub(IIncidentService incidents) : Hub
{
    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    public Task SubscribeToIncident(string incidentId)
    {
        if (!Guid.TryParse(incidentId, out var parsedIncidentId))
        {
            throw new HubException("A valid incident ID is required.");
        }

        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            IncidentHubGroups.Incident(parsedIncidentId));
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    public Task UnsubscribeFromIncident(string incidentId)
    {
        if (!Guid.TryParse(incidentId, out var parsedIncidentId))
        {
            throw new HubException("A valid incident ID is required.");
        }

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            IncidentHubGroups.Incident(parsedIncidentId));
    }

    public async Task SubscribeToTrackingCode(string trackingCode)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, Context.ConnectionAborted);
        if (incident is null)
        {
            throw new HubException("A valid tracking code is required.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            IncidentHubGroups.Incident(incident.Id),
            Context.ConnectionAborted);
    }

    public async Task UnsubscribeFromTrackingCode(string trackingCode)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, Context.ConnectionAborted);
        if (incident is null)
        {
            throw new HubException("A valid tracking code is required.");
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            IncidentHubGroups.Incident(incident.Id),
            Context.ConnectionAborted);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    public Task SubscribeToOperations()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, IncidentHubGroups.Operations);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    public Task UnsubscribeFromOperations()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, IncidentHubGroups.Operations);
    }
}
