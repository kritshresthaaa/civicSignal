using CivicSignal.Application.Incidents.Models;

namespace CivicSignal.Application.Abstractions.Realtime;

public sealed record IncidentRealtimeEventDto(
    Guid IncidentId,
    string EventType,
    string IncidentStatus,
    string Message,
    DateTimeOffset OccurredAt,
    IncidentDto? Incident,
    IncidentMediaDto? Media,
    TriagePredictionDto? Prediction,
    IReadOnlyCollection<DuplicateCandidateDto> DuplicateCandidates,
    IncidentProcessingStatusDto ProcessingStatus);
