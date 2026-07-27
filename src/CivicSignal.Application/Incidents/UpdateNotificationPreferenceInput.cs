namespace CivicSignal.Application.Incidents;

public sealed record UpdateNotificationPreferenceInput(bool AlertsEnabled, string? Channel);
