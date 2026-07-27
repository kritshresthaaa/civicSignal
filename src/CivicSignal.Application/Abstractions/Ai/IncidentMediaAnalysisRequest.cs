namespace CivicSignal.Application.Abstractions.Ai;

public sealed record IncidentMediaAnalysisRequest(
    Guid IncidentId,
    IncidentMediaDescriptor Media,
    Stream Content);
