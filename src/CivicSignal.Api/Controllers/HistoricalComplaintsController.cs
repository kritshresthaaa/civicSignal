using CivicSignal.Api.Contracts.HistoricalComplaints;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Application.HistoricalComplaints.Models;
using CivicSignal.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Route("api/historical-complaints")]
public sealed class HistoricalComplaintsController(IHistoricalComplaintService historicalComplaints) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<HistoricalComplaintDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<HistoricalComplaintDto>>> Search(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] string? complaintType,
        [FromQuery] string? agency,
        [FromQuery] string? status,
        [FromQuery] string? borough,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusMeters,
        [FromQuery] DateTimeOffset? createdFrom,
        [FromQuery] DateTimeOffset? createdTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var results = await historicalComplaints.SearchAsync(
            new HistoricalComplaintSearchInput(
                query,
                category,
                complaintType,
                agency,
                status,
                borough,
                latitude,
                longitude,
                radiusMeters,
                createdFrom,
                createdTo,
                page,
                pageSize),
            cancellationToken);

        return Ok(results);
    }

    [HttpGet("summary")]
    [ProducesResponseType<HistoricalComplaintSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HistoricalComplaintSummaryDto>> GetSummary(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] string? complaintType,
        [FromQuery] string? agency,
        [FromQuery] string? status,
        [FromQuery] string? borough,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusMeters,
        [FromQuery] DateTimeOffset? createdFrom,
        [FromQuery] DateTimeOffset? createdTo,
        CancellationToken cancellationToken = default)
    {
        var summary = await historicalComplaints.GetSummaryAsync(
            new HistoricalComplaintSearchInput(
                query,
                category,
                complaintType,
                agency,
                status,
                borough,
                latitude,
                longitude,
                radiusMeters,
                createdFrom,
                createdTo,
                Page: 1,
                PageSize: 100),
            cancellationToken);

        return Ok(summary);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("nyc311/import")]
    [ProducesResponseType<HistoricalComplaintImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HistoricalComplaintImportResultDto>> ImportNyc311(
        [FromBody] ImportNyc311ComplaintsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await historicalComplaints.ImportNyc311Async(
            new ImportNyc311ComplaintsInput(
                request.Limit,
                request.DaysBack,
                request.ComplaintType,
                request.Borough),
            cancellationToken);

        return Ok(result);
    }
}
