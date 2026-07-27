namespace CivicSignal.Application.Agents.Models;

public sealed record DraftWorkOrderDto(
    string Title,
    string AgencyCode,
    string Priority,
    string Summary,
    IReadOnlyCollection<string> Evidence);
