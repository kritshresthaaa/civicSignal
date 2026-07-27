namespace CivicSignal.Api.Contracts.Incidents;

public sealed record UpdateNotificationPreferenceRequest(bool AlertsEnabled, string? Channel);
