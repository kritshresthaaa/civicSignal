namespace CivicSignal.Application.Incidents;

public sealed record AddIncidentMediaInput(
    string FileName,
    string ContentType,
    string StorageUri);
