using CivicSignal.Application.AiEvaluations;
using CivicSignal.Application.AiEvaluations.Models;
using CivicSignal.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Authorize(Policy = CivicSignalPolicies.IncidentReview)]
[Route("api/ai-evaluations")]
public sealed class AiEvaluationsController(IAiEvaluationService evaluations) : ControllerBase
{
    [HttpGet("baselines")]
    [ProducesResponseType<AiEvaluationBaselineReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiEvaluationBaselineReportDto>> GetBaselineReport(
        CancellationToken cancellationToken)
    {
        var report = await evaluations.GetBaselineReportAsync(cancellationToken);

        return Ok(report);
    }
}
