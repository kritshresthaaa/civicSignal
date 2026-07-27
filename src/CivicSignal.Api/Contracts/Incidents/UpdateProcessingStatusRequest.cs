namespace CivicSignal.Api.Contracts.Incidents;

public sealed record UpdateProcessingStatusRequest(
    string StepName,
    string Status,
    string? ErrorMessage);
