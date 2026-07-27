namespace CivicSignal.Api.Hubs;

internal static class IncidentHubGroups
{
    public const string Operations = "operations";

    public static string Incident(Guid incidentId)
    {
        return $"incident:{incidentId:D}";
    }
}
