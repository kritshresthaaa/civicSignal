using System.Security.Claims;
using CivicSignal.Api.Contracts.DataImports;
using CivicSignal.Application.DataImports;
using CivicSignal.Application.DataImports.Models;
using CivicSignal.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
[Route("api/data-import-jobs")]
public sealed class DataImportJobsController(IDataImportJobService dataImportJobs) : ControllerBase
{
    [HttpPost("nyc311")]
    [ProducesResponseType<DataImportJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DataImportJobDto>> QueueNyc311Import(
        [FromBody] CreateNyc311ImportJobRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var job = await dataImportJobs.QueueNyc311ImportAsync(
            new CreateNyc311ImportJobInput(
                request.Limit,
                request.DaysBack,
                request.ComplaintType,
                request.Borough,
                Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null),
            cancellationToken);

        return AcceptedAtAction(nameof(GetById), new { jobId = job.Id }, job);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<DataImportJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<DataImportJobDto>>> Search(
        [FromQuery] string? source,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await dataImportJobs.SearchAsync(
            new DataImportJobSearchInput(source, status, page, pageSize),
            cancellationToken);

        return Ok(results);
    }

    [HttpGet("{jobId:guid}")]
    [ProducesResponseType<DataImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataImportJobDto>> GetById(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await dataImportJobs.GetByIdAsync(jobId, cancellationToken);

        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost("{jobId:guid}/retry")]
    [ProducesResponseType<DataImportJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataImportJobDto>> Retry(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await dataImportJobs.RetryAsync(jobId, cancellationToken);

        return AcceptedAtAction(nameof(GetById), new { jobId = job.Id }, job);
    }
}
