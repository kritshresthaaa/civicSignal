namespace CivicSignal.Api.Contracts.Incidents;

public sealed class CreateIncidentRequest
{
    public string Description { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
