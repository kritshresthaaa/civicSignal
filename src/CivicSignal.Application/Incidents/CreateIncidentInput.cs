namespace CivicSignal.Application.Incidents;

public sealed record CreateIncidentInput(
    string Description,
    double Latitude,
    double Longitude);
