namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentAnalysisRequest(
    Guid IncidentId,
    string Description,
    double Latitude,
    double Longitude,
    IReadOnlyCollection<IncidentMediaDescriptor> Media);
