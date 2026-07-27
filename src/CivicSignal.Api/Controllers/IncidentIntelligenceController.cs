using CivicSignal.Api.Contracts.Incidents;
using CivicSignal.Application.Agents;
using CivicSignal.Application.Agents.Models;
using CivicSignal.Application.Identity;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Route("api/incidents/{incidentId:guid}")]
public sealed class IncidentIntelligenceController(
    IIncidentIntelligenceService intelligence,
    IControlledTriageAgentService agentWorkflow,
    IFileStorageService fileStorage) : ControllerBase
{
    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("media/upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IncidentMediaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentMediaDto>> UploadMedia(
        Guid incidentId,
        [FromForm] UploadIncidentMediaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = "A non-empty media file is required."
            });
        }

        await using var stream = request.File.OpenReadStream();
        var storedFile = await fileStorage.StoreAsync(
            stream,
            request.File.FileName,
            request.File.ContentType,
            cancellationToken);

        var media = await intelligence.AddMediaAsync(
            incidentId,
            new AddIncidentMediaInput(storedFile.FileName, storedFile.ContentType, storedFile.StorageUri),
            cancellationToken);

        return CreatedAtAction(nameof(GetMedia), new { incidentId }, media);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("media")]
    [ProducesResponseType<IncidentMediaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentMediaDto>> AddMedia(
        Guid incidentId,
        [FromBody] AddIncidentMediaRequest request,
        CancellationToken cancellationToken)
    {
        var media = await intelligence.AddMediaAsync(
            incidentId,
            new AddIncidentMediaInput(request.FileName, request.ContentType, request.StorageUri),
            cancellationToken);

        return CreatedAtAction(nameof(GetMedia), new { incidentId }, media);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("media")]
    [ProducesResponseType<IReadOnlyCollection<IncidentMediaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<IncidentMediaDto>>> GetMedia(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var media = await intelligence.GetMediaAsync(incidentId, cancellationToken);

        return media is null ? NotFound() : Ok(media);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("analyze")]
    [ProducesResponseType<TriagePredictionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriagePredictionDto>> Analyze(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var prediction = await intelligence.AnalyzeAsync(incidentId, cancellationToken);

        return Ok(prediction);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("agent-workflow")]
    [ProducesResponseType<ControlledTriageWorkflowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ControlledTriageWorkflowDto>> RunAgentWorkflow(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var workflow = await agentWorkflow.RunAsync(incidentId, cancellationToken);

        return Ok(workflow);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("prediction")]
    [ProducesResponseType<TriagePredictionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriagePredictionDto>> GetLatestPrediction(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var prediction = await intelligence.GetLatestPredictionAsync(incidentId, cancellationToken);

        return prediction is null ? NoContent() : Ok(prediction);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("duplicates")]
    [ProducesResponseType<IReadOnlyCollection<DuplicateCandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<DuplicateCandidateDto>>> GetDuplicateCandidates(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var candidates = await intelligence.GetDuplicateCandidatesAsync(incidentId, cancellationToken);

        return candidates is null ? NotFound() : Ok(candidates);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("similar")]
    [ProducesResponseType<IReadOnlyCollection<DuplicateCandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<DuplicateCandidateDto>>> GetSimilarIncidents(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var candidates = await intelligence.GetDuplicateCandidatesAsync(incidentId, cancellationToken);

        return candidates is null ? NotFound() : Ok(candidates);
    }
}
