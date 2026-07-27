namespace CivicSignal.Application.Abstractions.Realtime;

public static class IncidentRealtimeEventTypes
{
    public const string IncidentCreated = "incident.created";
    public const string MediaAdded = "incident.mediaAdded";
    public const string MediaAnalyzed = "incident.mediaAnalyzed";
    public const string Analyzed = "incident.analyzed";
    public const string Reviewed = "incident.reviewed";
    public const string Assigned = "incident.assigned";
    public const string Dispatched = "incident.dispatched";
    public const string DuplicateLinked = "incident.duplicateLinked";
    public const string ProcessingStatusChanged = "incident.processingStatusChanged";
    public const string UpdateRequested = "incident.updateRequested";
    public const string NotificationPreferenceUpdated = "incident.notificationPreferenceUpdated";
    public const string FeedbackReceived = "incident.feedbackReceived";
}
