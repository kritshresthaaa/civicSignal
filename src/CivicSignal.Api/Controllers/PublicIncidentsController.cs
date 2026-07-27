using CivicSignal.Api.Contracts.Incidents;
using CivicSignal.Api.Security;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicSignal.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/public/incidents")]
public sealed class PublicIncidentsController(
    IIncidentService incidents,
    IIncidentIntelligenceService intelligence,
    IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<PublicIncidentFeedItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PublicIncidentFeedItemResponse>>> SearchRecent(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await incidents.SearchAsync(
            new IncidentSearchInput(status, Math.Max(1, page), Math.Clamp(pageSize, 1, 50)),
            cancellationToken);

        var response = new List<PublicIncidentFeedItemResponse>(results.Count);
        foreach (var incident in results.OrderByDescending(incident => incident.CreatedAt))
        {
            var prediction = await intelligence.GetLatestPredictionAsync(incident.Id, cancellationToken);
            var media = await intelligence.GetMediaAsync(incident.Id, cancellationToken);
            var feedback = await incidents.GetFeedbackAsync(incident.Id, cancellationToken);
            response.Add(ToPublicFeedItem(incident, prediction, media ?? [], feedback ?? []));
        }

        return Ok(response);
    }

    [HttpGet("{trackingCode}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> GetByTrackingCode(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);

        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpGet("{trackingCode}/status")]
    [ProducesResponseType<IncidentProcessingStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentProcessingStatusDto>> GetProcessingStatus(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var status = await incidents.GetProcessingStatusAsync(incident.Id, cancellationToken);

        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("{trackingCode}/media")]
    [ProducesResponseType<IReadOnlyCollection<IncidentMediaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<IncidentMediaDto>>> GetMedia(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var media = await intelligence.GetMediaAsync(incident.Id, cancellationToken);

        return media is null ? NotFound() : Ok(media);
    }

    [HttpPost("{trackingCode}/media/upload")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IncidentMediaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentMediaDto>> UploadMedia(
        string trackingCode,
        [FromForm] UploadIncidentMediaRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

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
            incident.Id,
            new AddIncidentMediaInput(storedFile.FileName, storedFile.ContentType, storedFile.StorageUri),
            cancellationToken);

        return CreatedAtAction(nameof(GetMedia), new { trackingCode = incident.TrackingCode }, media);
    }

    [HttpGet("{trackingCode}/prediction")]
    [ProducesResponseType<TriagePredictionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriagePredictionDto>> GetLatestPrediction(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var prediction = await intelligence.GetLatestPredictionAsync(incident.Id, cancellationToken);

        return prediction is null ? NoContent() : Ok(prediction);
    }

    [HttpGet("{trackingCode}/duplicates")]
    [ProducesResponseType<IReadOnlyCollection<DuplicateCandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<DuplicateCandidateDto>>> GetDuplicateCandidates(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var candidates = await intelligence.GetDuplicateCandidatesAsync(incident.Id, cancellationToken);

        return candidates is null ? NotFound() : Ok(candidates);
    }

    [HttpGet("{trackingCode}/similar")]
    [ProducesResponseType<IReadOnlyCollection<DuplicateCandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<DuplicateCandidateDto>>> GetSimilarIncidents(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var candidates = await intelligence.GetDuplicateCandidatesAsync(incident.Id, cancellationToken);

        return candidates is null ? NotFound() : Ok(candidates);
    }

    [HttpPost("{trackingCode}/update-requests")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<IncidentUpdateRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentUpdateRequestDto>> RequestUpdate(
        string trackingCode,
        [FromBody] CreateUpdateRequestRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var updateRequest = await incidents.RequestUpdateAsync(
            incident.Id,
            new CreateIncidentUpdateRequestInput(request.Message),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, updateRequest);
    }

    [HttpPut("{trackingCode}/notification-preference")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<IncidentNotificationPreferenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentNotificationPreferenceDto>> UpdateNotificationPreference(
        string trackingCode,
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var preference = await incidents.UpdateNotificationPreferenceAsync(
            incident.Id,
            new UpdateNotificationPreferenceInput(request.AlertsEnabled, request.Channel),
            cancellationToken);

        return Ok(preference);
    }

    [HttpPost("{trackingCode}/feedback")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<IncidentFeedbackDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentFeedbackDto>> AddFeedback(
        string trackingCode,
        [FromBody] CreateIncidentFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var feedback = await incidents.AddFeedbackAsync(
            incident.Id,
            new CreateIncidentFeedbackInput(request.Rating, request.Comment),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, feedback);
    }

    [HttpGet("{trackingCode}/feedback")]
    [ProducesResponseType<IReadOnlyCollection<IncidentFeedbackDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<IncidentFeedbackDto>>> GetFeedback(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByTrackingCodeAsync(trackingCode, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var feedback = await incidents.GetFeedbackAsync(incident.Id, cancellationToken);

        return feedback is null ? NotFound() : Ok(feedback);
    }

    private static PublicIncidentFeedItemResponse ToPublicFeedItem(
        IncidentDto incident,
        TriagePredictionDto? prediction,
        IReadOnlyCollection<IncidentMediaDto> media,
        IReadOnlyCollection<IncidentFeedbackDto> feedback)
    {
        var latitude = RoundPublicCoordinate(incident.Latitude);
        var longitude = RoundPublicCoordinate(incident.Longitude);
        var latestImage = media
            .Where(IsImageMedia)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var latestMediaSummary = media
            .OrderByDescending(item => item.AnalyzedAt ?? item.CreatedAt)
            .Select(item => item.AnalysisSummary ?? item.Transcript)
            .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary));

        return new PublicIncidentFeedItemResponse(
            incident.TrackingCode,
            incident.Description,
            latitude,
            longitude,
            incident.Status,
            incident.CreatedAt,
            incident.CorrectedCategory ?? prediction?.Category ?? "GeneralIncident",
            incident.CorrectedSeverity ?? prediction?.Severity ?? "Medium",
            incident.CorrectedAgencyCode ?? incident.AssignedAgencyCode ?? prediction?.SuggestedAgencyCode,
            incident.ReviewedAt is not null || !string.IsNullOrWhiteSpace(incident.ReviewDecision),
            incident.DuplicateOfIncidentId is not null,
            $"Near {latitude:0.000}, {longitude:0.000}",
            media.Count,
            feedback.Count(item => item.Rating >= 5),
            feedback.Count(item => !string.IsNullOrWhiteSpace(item.Comment)),
            latestImage?.StorageUri,
            latestMediaSummary);
    }

    private static bool IsImageMedia(IncidentMediaDto media)
    {
        return media.MediaType.Equals("Image", StringComparison.OrdinalIgnoreCase)
            || media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static double RoundPublicCoordinate(double coordinate)
    {
        return Math.Round(coordinate, 3, MidpointRounding.AwayFromZero);
    }
}
