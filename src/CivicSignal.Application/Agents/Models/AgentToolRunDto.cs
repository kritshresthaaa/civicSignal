namespace CivicSignal.Application.Agents.Models;

public sealed record AgentToolRunDto(
    string ToolName,
    string Status,
    string InputSummary,
    string OutputSummary,
    double? Confidence,
    DateTimeOffset CompletedAt);
