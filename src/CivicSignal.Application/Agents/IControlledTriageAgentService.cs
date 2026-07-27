using CivicSignal.Application.Agents.Models;

namespace CivicSignal.Application.Agents;

public interface IControlledTriageAgentService
{
    Task<ControlledTriageWorkflowDto> RunAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);
}
