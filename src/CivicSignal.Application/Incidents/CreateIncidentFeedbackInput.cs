namespace CivicSignal.Application.Incidents;

public sealed record CreateIncidentFeedbackInput(int Rating, string? Comment);
