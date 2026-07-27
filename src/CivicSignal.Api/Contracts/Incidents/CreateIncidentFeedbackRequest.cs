namespace CivicSignal.Api.Contracts.Incidents;

public sealed record CreateIncidentFeedbackRequest(int Rating, string? Comment = null);
