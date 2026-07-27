using System.Security.Claims;
using CivicSignal.Api.Contracts.Incidents;
using CivicSignal.Api.Security;
using CivicSignal.Application.Identity;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController(IIncidentService incidents) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentDto>> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateIncidentInput(
            request.Description,
            request.Latitude,
            request.Longitude);

        var incident = await incidents.CreateAsync(input, cancellationToken);

        return Created($"/api/public/incidents/{Uri.EscapeDataString(incident.TrackingCode)}", incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("{incidentId:guid}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> GetById(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);

        return incident is null ? NotFound() : Ok(incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<IncidentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<IncidentDto>>> Search(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var results = await incidents.SearchAsync(new IncidentSearchInput(status, page, pageSize), cancellationToken);

        return Ok(results);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("{incidentId:guid}/status")]
    [ProducesResponseType<IncidentProcessingStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentProcessingStatusDto>> GetProcessingStatus(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var status = await incidents.GetProcessingStatusAsync(incidentId, cancellationToken);

        return status is null ? NotFound() : Ok(status);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("{incidentId:guid}/processing-status")]
    [ProducesResponseType<IncidentProcessingStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentProcessingStatusDto>> UpdateProcessingStatus(
        Guid incidentId,
        [FromBody] UpdateProcessingStatusRequest request,
        CancellationToken cancellationToken)
    {
        var status = await incidents.UpdateProcessingStatusAsync(
            incidentId,
            new UpdateProcessingStatusInput(request.StepName, request.Status, request.ErrorMessage),
            cancellationToken);

        return Ok(status);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("{incidentId:guid}/assign")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> Assign(
        Guid incidentId,
        [FromBody] AssignIncidentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var incident = await incidents.AssignAsync(
            incidentId,
            new AssignIncidentInput(
                request.AssignedTeam,
                userId,
                request.AssignedAgencyCode,
                request.Note),
            cancellationToken);

        return Ok(incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("{incidentId:guid}/dispatch")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> Dispatch(
        Guid incidentId,
        [FromBody] DispatchIncidentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var incident = await incidents.DispatchAsync(
            incidentId,
            new DispatchIncidentInput(userId, request.Note),
            cancellationToken);

        return Ok(incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentOperations)]
    [HttpPost("{incidentId:guid}/mark-duplicate")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> MarkDuplicate(
        Guid incidentId,
        [FromBody] LinkDuplicateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var incident = await incidents.LinkDuplicateAsync(
            incidentId,
            new LinkDuplicateIncidentInput(request.DuplicateOfIncidentId, userId, request.Note),
            cancellationToken);

        return Ok(incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpPost("{incidentId:guid}/update-requests")]
    [ProducesResponseType<IncidentUpdateRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentUpdateRequestDto>> RequestUpdate(
        Guid incidentId,
        [FromBody] CreateUpdateRequestRequest request,
        CancellationToken cancellationToken)
    {
        var updateRequest = await incidents.RequestUpdateAsync(
            incidentId,
            new CreateIncidentUpdateRequestInput(request.Message),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, updateRequest);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpPut("{incidentId:guid}/notification-preference")]
    [ProducesResponseType<IncidentNotificationPreferenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentNotificationPreferenceDto>> UpdateNotificationPreference(
        Guid incidentId,
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var preference = await incidents.UpdateNotificationPreferenceAsync(
            incidentId,
            new UpdateNotificationPreferenceInput(request.AlertsEnabled, request.Channel),
            cancellationToken);

        return Ok(preference);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpPost("{incidentId:guid}/feedback")]
    [ProducesResponseType<IncidentFeedbackDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentFeedbackDto>> AddFeedback(
        Guid incidentId,
        [FromBody] CreateIncidentFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var feedback = await incidents.AddFeedbackAsync(
            incidentId,
            new CreateIncidentFeedbackInput(request.Rating, request.Comment),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, feedback);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpPost("{incidentId:guid}/review")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> Review(
        Guid incidentId,
        [FromBody] ReviewIncidentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var reviewerId))
        {
            return Unauthorized();
        }

        var incident = await incidents.ReviewAsync(
            incidentId,
            new ReviewIncidentInput(
                request.Decision,
                request.Note,
                reviewerId,
                request.CorrectedCategory,
                request.CorrectedAgencyCode,
                request.CorrectedSeverity,
                request.DuplicateOfIncidentId,
                request.AcceptedPrediction),
            cancellationToken);

        return Ok(incident);
    }

    [Authorize(Policy = CivicSignalPolicies.IncidentReview)]
    [HttpGet("{incidentId:guid}/reviews")]
    [ProducesResponseType<IReadOnlyCollection<IncidentReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<IncidentReviewDto>>> GetReviewHistory(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var reviews = await incidents.GetReviewHistoryAsync(incidentId, cancellationToken);

        return reviews is null ? NotFound() : Ok(reviews);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }
}
