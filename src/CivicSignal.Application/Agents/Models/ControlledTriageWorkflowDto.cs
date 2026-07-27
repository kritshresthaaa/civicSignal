namespace CivicSignal.Application.Agents.Models;

public sealed record ControlledTriageWorkflowDto(
    Guid IncidentId,
    string Status,
    bool RequiresHumanReview,
    string? ReviewReason,
    double SlaRisk,
    WeatherContextDto? Weather,
    DraftWorkOrderDto? DraftWorkOrder,
    IReadOnlyCollection<AgentToolRunDto> ToolRuns);
