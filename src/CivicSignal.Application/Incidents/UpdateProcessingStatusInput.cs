namespace CivicSignal.Application.Incidents;

public sealed record UpdateProcessingStatusInput(
    string StepName,
    string Status,
    string? ErrorMessage);
