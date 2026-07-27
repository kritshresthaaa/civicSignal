namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentNotificationPreferenceDto(
    Guid IncidentId,
    bool AlertsEnabled,
    string Channel,
    DateTimeOffset UpdatedAt);
