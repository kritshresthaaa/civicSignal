namespace CivicSignal.Api.Contracts.Incidents;

public sealed class AddIncidentMediaRequest
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StorageUri { get; set; } = string.Empty;
}
