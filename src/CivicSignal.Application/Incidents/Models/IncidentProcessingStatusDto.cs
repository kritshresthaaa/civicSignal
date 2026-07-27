namespace CivicSignal.Application.Incidents.Models;

public sealed record IncidentProcessingStatusDto(
    Guid IncidentId,
    string IncidentStatus,
    IReadOnlyCollection<ProcessingStepDto> Steps);
