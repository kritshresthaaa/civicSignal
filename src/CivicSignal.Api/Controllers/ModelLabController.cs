using CivicSignal.Api.Contracts.ModelLab;
using CivicSignal.Application.ModelLab;
using CivicSignal.Application.ModelLab.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/model-lab")]
public sealed class ModelLabController(IModelLabService modelLab) : ControllerBase
{
    [HttpPost("analyze")]
    [ProducesResponseType<ModelLabAnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModelLabAnalysisDto>> Analyze(
        [FromBody] AnalyzeTextRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = "Model Lab text is required."
            });
        }

        var analysis = await modelLab.AnalyzeAsync(
            new ModelLabAnalysisInput(request.Text, request.EmbeddingDimensions),
            cancellationToken);

        return Ok(analysis);
    }
}
