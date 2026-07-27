using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Realtime;
using CivicSignal.Application.Common;
using CivicSignal.Application.Incidents.Models;
using CivicSignal.Domain.Incidents;
using CivicSignal.Domain.Incidents.ValueObjects;
using FluentValidation;

namespace CivicSignal.Application.Incidents;

internal sealed class IncidentService(
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IClock clock,
    ITextEmbeddingGenerator textEmbeddings,
    IApplicationCache cache,
    IIncidentProcessingQueue processingQueue,
    IIncidentRealtimeNotifier realtime,
    IValidator<CreateIncidentInput> createIncidentValidator,
    IValidator<ReviewIncidentInput> reviewIncidentValidator,
    IValidator<AssignIncidentInput> assignIncidentValidator,
    IValidator<DispatchIncidentInput> dispatchIncidentValidator,
    IValidator<LinkDuplicateIncidentInput> linkDuplicateIncidentValidator,
    IValidator<UpdateProcessingStatusInput> updateProcessingStatusValidator,
    IValidator<CreateIncidentUpdateRequestInput> createIncidentUpdateRequestValidator,
    IValidator<UpdateNotificationPreferenceInput> updateNotificationPreferenceValidator,
    IValidator<CreateIncidentFeedbackInput> createIncidentFeedbackValidator) : IIncidentService
{
    private static readonly TimeSpan IncidentCacheDuration = TimeSpan.FromMinutes(5);
    private const int MaxTrackingCodeAttempts = 8;

    public async Task<IncidentDto> CreateAsync(CreateIncidentInput input, CancellationToken cancellationToken = default)
    {
        await createIncidentValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await CreateIncidentWithUniqueTrackingCodeAsync(
            input.Description,
            new GeoPoint(input.Latitude, input.Longitude),
            cancellationToken);
        var embedding = await textEmbeddings.GenerateEmbeddingAsync(input.Description, cancellationToken);

        await incidents.AddAsync(incident, cancellationToken);
        incidents.SetTextEmbedding(incident, embedding);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var incidentDto = incident.ToDto();
        await CacheIncidentAsync(incidentDto, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.IncidentCreated,
                statusDto.IncidentStatus,
                "Incident submitted through the citizen portal.",
                clock.UtcNow,
                incidentDto,
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        await processingQueue.EnqueueAsync(
            incident.Id,
            "IncidentCreated",
            cancellationToken);

        return incidentDto;
    }

    public async Task<IncidentDto?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildIncidentCacheKey(incidentId);
        var cachedIncident = await cache.GetAsync<IncidentDto>(cacheKey, cancellationToken);
        if (cachedIncident is not null)
        {
            return cachedIncident;
        }

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);
        var incidentDto = incident?.ToDto();

        if (incidentDto is not null)
        {
            await CacheIncidentAsync(incidentDto, cancellationToken);
        }

        return incidentDto;
    }

    public async Task<IncidentDto?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        string normalizedTrackingCode;
        try
        {
            normalizedTrackingCode = Incident.NormalizePublicTrackingCode(trackingCode);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var cacheKey = BuildTrackingCodeCacheKey(normalizedTrackingCode);
        var cachedIncident = await cache.GetAsync<IncidentDto>(cacheKey, cancellationToken);
        if (cachedIncident is not null)
        {
            return cachedIncident;
        }

        var incident = await incidents.GetByPublicTrackingCodeAsync(normalizedTrackingCode, cancellationToken);
        var incidentDto = incident?.ToDto();

        if (incidentDto is not null)
        {
            await CacheIncidentAsync(incidentDto, cancellationToken);
        }

        return incidentDto;
    }

    public async Task<IReadOnlyCollection<IncidentDto>> SearchAsync(
        IncidentSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var criteria = new IncidentSearchCriteria(
            input.Status,
            Math.Max(1, input.Page),
            Math.Clamp(input.PageSize, 1, 200));

        var results = await incidents.SearchAsync(criteria, cancellationToken);
        return results.Select(incident => incident.ToDto()).ToArray();
    }

    public async Task<IncidentDto> ReviewAsync(
        Guid incidentId,
        ReviewIncidentInput input,
        CancellationToken cancellationToken = default)
    {
        await reviewIncidentValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var decision = Enum.Parse<ReviewDecision>(input.Decision, ignoreCase: true);
        var correctedSeverity = string.IsNullOrWhiteSpace(input.CorrectedSeverity)
            ? (IncidentSeverity?)null
            : Enum.Parse<IncidentSeverity>(input.CorrectedSeverity, ignoreCase: true);

        incident.Review(
            decision,
            input.ReviewerUserId,
            input.Note,
            clock.UtcNow,
            string.IsNullOrWhiteSpace(input.CorrectedCategory) ? null : new IncidentCategory(input.CorrectedCategory),
            string.IsNullOrWhiteSpace(input.CorrectedAgencyCode) ? null : new AgencyCode(input.CorrectedAgencyCode),
            correctedSeverity,
            input.DuplicateOfIncidentId,
            input.AcceptedPrediction);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var incidentDto = incident.ToDto();
        await CacheIncidentAsync(incidentDto, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.Reviewed,
                statusDto.IncidentStatus,
                $"Review decision recorded: {incidentDto.ReviewDecision}.",
                clock.UtcNow,
                incidentDto,
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        return incidentDto;
    }

    public async Task<IncidentDto> AssignAsync(
        Guid incidentId,
        AssignIncidentInput input,
        CancellationToken cancellationToken = default)
    {
        await assignIncidentValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        AgencyCode? assignedAgency = string.IsNullOrWhiteSpace(input.AssignedAgencyCode)
            ? null
            : new AgencyCode(input.AssignedAgencyCode);

        incident.Assign(
            input.AssignedTeam,
            input.AssignedByUserId,
            clock.UtcNow,
            assignedAgency);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var incidentDto = incident.ToDto();
        await CacheIncidentAsync(incidentDto, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.Assigned,
                statusDto.IncidentStatus,
                BuildStaffActionMessage(input.Note, $"Incident assigned to {incident.AssignedTeam}."),
                clock.UtcNow,
                incidentDto,
                null,
                null,
                incident.DuplicateCandidates.Select(candidate => candidate.ToDto()).ToArray(),
                statusDto),
            cancellationToken);

        return incidentDto;
    }

    public async Task<IncidentDto> DispatchAsync(
        Guid incidentId,
        DispatchIncidentInput input,
        CancellationToken cancellationToken = default)
    {
        await dispatchIncidentValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        incident.Dispatch(input.DispatchedByUserId, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var incidentDto = incident.ToDto();
        await CacheIncidentAsync(incidentDto, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.Dispatched,
                statusDto.IncidentStatus,
                BuildStaffActionMessage(input.Note, "Incident dispatched to field operations."),
                clock.UtcNow,
                incidentDto,
                null,
                null,
                incident.DuplicateCandidates.Select(candidate => candidate.ToDto()).ToArray(),
                statusDto),
            cancellationToken);

        return incidentDto;
    }

    public async Task<IncidentDto> LinkDuplicateAsync(
        Guid incidentId,
        LinkDuplicateIncidentInput input,
        CancellationToken cancellationToken = default)
    {
        await linkDuplicateIncidentValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        _ = await incidents.GetByIdAsync(input.DuplicateOfIncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), input.DuplicateOfIncidentId);

        incident.LinkDuplicate(input.DuplicateOfIncidentId, input.LinkedByUserId, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var incidentDto = incident.ToDto();
        await CacheIncidentAsync(incidentDto, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.DuplicateLinked,
                statusDto.IncidentStatus,
                BuildStaffActionMessage(input.Note, $"Incident linked as duplicate of {input.DuplicateOfIncidentId}."),
                clock.UtcNow,
                incidentDto,
                null,
                null,
                incident.DuplicateCandidates.Select(candidate => candidate.ToDto()).ToArray(),
                statusDto),
            cancellationToken);

        return incidentDto;
    }

    public async Task<IReadOnlyCollection<IncidentReviewDto>?> GetReviewHistoryAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);

        return incident?.ReviewRecords
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => review.ToDto())
            .ToArray();
    }

    public async Task<IncidentProcessingStatusDto?> GetProcessingStatusAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);
        return incident?.ToProcessingStatusDto();
    }

    public async Task<IncidentProcessingStatusDto> UpdateProcessingStatusAsync(
        Guid incidentId,
        UpdateProcessingStatusInput input,
        CancellationToken cancellationToken = default)
    {
        await updateProcessingStatusValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var status = Enum.Parse<ProcessingStepStatus>(input.Status, ignoreCase: true);
        switch (status)
        {
            case ProcessingStepStatus.InProgress:
                incident.StartProcessingStep(input.StepName, clock.UtcNow);
                break;
            case ProcessingStepStatus.Succeeded:
                incident.CompleteProcessingStep(input.StepName, clock.UtcNow);
                break;
            case ProcessingStepStatus.Failed:
                incident.FailProcessingStep(input.StepName, input.ErrorMessage!, clock.UtcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(input), input.Status, "Processing status is not supported.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveIncidentCacheAsync(incident, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.ProcessingStatusChanged,
                statusDto.IncidentStatus,
                $"{input.StepName} is {status}.",
                clock.UtcNow,
                null,
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        return statusDto;
    }

    public async Task<IncidentUpdateRequestDto> RequestUpdateAsync(
        Guid incidentId,
        CreateIncidentUpdateRequestInput input,
        CancellationToken cancellationToken = default)
    {
        await createIncidentUpdateRequestValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var updateRequest = incident.RequestUpdate(input.Message, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveIncidentCacheAsync(incident, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.UpdateRequested,
                statusDto.IncidentStatus,
                "Citizen requested a status update.",
                clock.UtcNow,
                incident.ToDto(),
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        return updateRequest.ToDto();
    }

    public async Task<IncidentNotificationPreferenceDto> UpdateNotificationPreferenceAsync(
        Guid incidentId,
        UpdateNotificationPreferenceInput input,
        CancellationToken cancellationToken = default)
    {
        await updateNotificationPreferenceValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        incident.UpdateNotificationPreference(input.AlertsEnabled, input.Channel, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveIncidentCacheAsync(incident, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.NotificationPreferenceUpdated,
                statusDto.IncidentStatus,
                input.AlertsEnabled ? "Citizen enabled status alerts." : "Citizen disabled status alerts.",
                clock.UtcNow,
                incident.ToDto(),
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        return incident.ToNotificationPreferenceDto();
    }

    public async Task<IReadOnlyCollection<IncidentFeedbackDto>?> GetFeedbackAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);

        return incident?.FeedbackItems
            .OrderByDescending(feedback => feedback.CreatedAt)
            .Select(feedback => feedback.ToDto())
            .ToArray();
    }

    public async Task<IncidentFeedbackDto> AddFeedbackAsync(
        Guid incidentId,
        CreateIncidentFeedbackInput input,
        CancellationToken cancellationToken = default)
    {
        await createIncidentFeedbackValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var feedback = incident.AddFeedback(input.Rating, input.Comment, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveIncidentCacheAsync(incident, cancellationToken);

        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.FeedbackReceived,
                statusDto.IncidentStatus,
                $"Citizen submitted {feedback.Rating}/5 status feedback.",
                clock.UtcNow,
                incident.ToDto(),
                null,
                null,
                [],
                statusDto),
            cancellationToken);

        return feedback.ToDto();
    }

    private Task CacheIncidentAsync(IncidentDto incident, CancellationToken cancellationToken)
    {
        return Task.WhenAll(
            cache.SetAsync(
                BuildIncidentCacheKey(incident.Id),
                incident,
                IncidentCacheDuration,
                cancellationToken),
            cache.SetAsync(
                BuildTrackingCodeCacheKey(incident.TrackingCode),
                incident,
                IncidentCacheDuration,
                cancellationToken));
    }

    private Task RemoveIncidentCacheAsync(Incident incident, CancellationToken cancellationToken)
    {
        return Task.WhenAll(
            cache.RemoveAsync(BuildIncidentCacheKey(incident.Id), cancellationToken),
            cache.RemoveAsync(BuildTrackingCodeCacheKey(incident.PublicTrackingCode), cancellationToken));
    }

    private static string BuildStaffActionMessage(string? note, string fallback)
    {
        return string.IsNullOrWhiteSpace(note) ? fallback : note.Trim();
    }

    private static string BuildIncidentCacheKey(Guid incidentId)
    {
        return $"incidents:by-id:{incidentId:N}";
    }

    private static string BuildTrackingCodeCacheKey(string trackingCode)
    {
        return $"incidents:by-tracking-code:{trackingCode}";
    }

    private async Task<Incident> CreateIncidentWithUniqueTrackingCodeAsync(
        string description,
        GeoPoint location,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTrackingCodeAttempts; attempt += 1)
        {
            var trackingCode = Incident.GeneratePublicTrackingCode();
            if (await incidents.PublicTrackingCodeExistsAsync(trackingCode, cancellationToken))
            {
                continue;
            }

            return Incident.Create(description, location, clock.UtcNow, trackingCode);
        }

        throw new InvalidOperationException("Could not allocate a unique public tracking code.");
    }
}
