namespace CivicSignal.Domain.Incidents;

public enum IncidentMediaAnalysisStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3,
    Skipped = 4
}
