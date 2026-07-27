using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Realtime;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Common;
using CivicSignal.Application.Incidents.Models;
using CivicSignal.Domain.Incidents;
using CivicSignal.Domain.Incidents.ValueObjects;
using FluentValidation;

namespace CivicSignal.Application.Incidents;

internal sealed class IncidentIntelligenceService(
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAiIncidentAnalyzer analyzer,
    IIncidentMediaAnalyzer mediaAnalyzer,
    IDuplicateIncidentSearchService duplicateSearch,
    IFileStorageService fileStorage,
    IIncidentProcessingQueue processingQueue,
    IIncidentRealtimeNotifier realtime,
    IValidator<AddIncidentMediaInput> addMediaValidator) : IIncidentIntelligenceService
{
    public async Task<IncidentMediaDto> AddMediaAsync(
        Guid incidentId,
        AddIncidentMediaInput input,
        CancellationToken cancellationToken = default)
    {
        await addMediaValidator.ValidateAndThrowAsync(input, cancellationToken);

        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var media = incident.AddMedia(input.FileName, input.ContentType, input.StorageUri, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var mediaDto = media.ToDto();
        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.MediaAdded,
                statusDto.IncidentStatus,
                "Evidence uploaded for incident review.",
                clock.UtcNow,
                null,
                mediaDto,
                null,
                [],
                statusDto),
            cancellationToken);

        await processingQueue.EnqueueAsync(
            incident.Id,
            "IncidentMediaUploaded",
            cancellationToken);

        return mediaDto;
    }

    public async Task<IReadOnlyCollection<IncidentMediaDto>?> GetMediaAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);

        return incident?.MediaItems
            .OrderByDescending(media => media.CreatedAt)
            .Select(media => media.ToDto())
            .ToArray();
    }

    public async Task<IncidentMediaDto> AnalyzeMediaAsync(
        Guid incidentId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var media = incident.MediaItems.SingleOrDefault(item => item.Id == mediaId)
            ?? throw new NotFoundException(nameof(IncidentMedia), mediaId);

        if (!CanAnalyzeMedia(media))
        {
            media.SkipAnalysis(
                $"Media type '{media.ContentType}' is stored for review but is not analyzed by the current AI pipeline.",
                clock.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PublishMediaAnalysisEventAsync(incident, media, "Media stored for reviewer inspection.", cancellationToken);

            return media.ToDto();
        }

        media.StartAnalysis(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishMediaAnalysisEventAsync(incident, media, "Media analysis started.", cancellationToken);

        await using var content = await fileStorage.OpenReadAsync(media.StorageUri, cancellationToken);
        if (content is null)
        {
            media.FailAnalysis("Stored media could not be opened for AI analysis.", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PublishMediaAnalysisEventAsync(incident, media, "Media analysis failed.", cancellationToken);

            return media.ToDto();
        }

        try
        {
            var result = await mediaAnalyzer.AnalyzeAsync(
                new IncidentMediaAnalysisRequest(
                    incident.Id,
                    BuildMediaDescriptor(media),
                    content),
                cancellationToken);

            media.CompleteAnalysis(
                result.Summary,
                result.Transcript,
                result.DetectedLabels,
                result.Confidence,
                result.ModelName,
                result.ModelVersion,
                result.ProcessingTimeMilliseconds,
                clock.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PublishMediaAnalysisEventAsync(incident, media, "Media analysis completed.", cancellationToken);

            return media.ToDto();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            media.FailAnalysis(exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PublishMediaAnalysisEventAsync(incident, media, "Media analysis failed.", cancellationToken);

            return media.ToDto();
        }
    }

    public async Task<TriagePredictionDto> AnalyzeAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var request = BuildAnalysisRequest(incident);
        var analysis = await analyzer.AnalyzeAsync(request, cancellationToken);

        var prediction = incident.AddTriagePrediction(
            new IncidentCategory(analysis.Category),
            ParseSeverity(analysis.Severity),
            new ConfidenceScore(analysis.Confidence),
            new AgencyCode(analysis.SuggestedAgencyCode),
            analysis.Summary,
            analysis.ModelName,
            analysis.ModelVersion,
            analysis.PromptVersion,
            analysis.ProcessingTimeMilliseconds,
            clock.UtcNow);

        foreach (var evidence in analysis.Evidence ?? [])
        {
            prediction.AddEvidence(
                evidence.Kind,
                evidence.Title,
                evidence.Detail,
                evidence.Confidence,
                clock.UtcNow);
        }

        var duplicateCandidates = await duplicateSearch.FindDuplicatesAsync(
            request,
            analysis,
            cancellationToken);

        foreach (var candidate in duplicateCandidates.Where(candidate =>
                     candidate.CandidateIncidentId != incident.Id))
        {
            incident.AddDuplicateCandidate(
                candidate.CandidateIncidentId,
                new ConfidenceScore(candidate.SimilarityScore),
                candidate.Reason,
                clock.UtcNow);

            prediction.AddEvidence(
                "Duplicate",
                "Similar incident candidate",
                candidate.Reason ?? $"Similar incident {candidate.CandidateIncidentId}",
                candidate.SimilarityScore,
                clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var predictionDto = prediction.ToDto();
        var statusDto = incident.ToProcessingStatusDto();
        var candidateDtos = incident.DuplicateCandidates
            .OrderByDescending(candidate => candidate.SimilarityScore.Value)
            .ThenByDescending(candidate => candidate.UpdatedAt)
            .Select(candidate => candidate.ToDto())
            .ToArray();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.Analyzed,
                statusDto.IncidentStatus,
                "AI triage and duplicate analysis completed.",
                clock.UtcNow,
                null,
                null,
                predictionDto,
                candidateDtos,
                statusDto),
            cancellationToken);

        return predictionDto;
    }

    public async Task<TriagePredictionDto?> GetLatestPredictionAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        return incident.TriagePredictions
            .OrderByDescending(prediction => prediction.CreatedAt)
            .FirstOrDefault()
            ?.ToDto();
    }

    public async Task<IReadOnlyCollection<DuplicateCandidateDto>?> GetDuplicateCandidatesAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken);

        return incident?.DuplicateCandidates
            .OrderByDescending(candidate => candidate.SimilarityScore.Value)
            .ThenByDescending(candidate => candidate.UpdatedAt)
            .Select(candidate => candidate.ToDto())
            .ToArray();
    }

    private static IncidentAnalysisRequest BuildAnalysisRequest(Incident incident)
    {
        var media = incident.MediaItems
            .OrderBy(media => media.CreatedAt)
            .Select(BuildMediaDescriptor)
            .ToArray();

        return new IncidentAnalysisRequest(
            incident.Id,
            incident.Description,
            incident.Location.Latitude,
            incident.Location.Longitude,
            media);
    }

    private static IncidentMediaDescriptor BuildMediaDescriptor(IncidentMedia media)
    {
        return new IncidentMediaDescriptor(
            media.Id,
            media.FileName,
            media.ContentType,
            media.StorageUri,
            media.MediaType.ToString(),
            media.AnalysisStatus.ToString(),
            media.AnalysisSummary,
            media.Transcript,
            SplitLabels(media.DetectedLabels));
    }

    private async Task PublishMediaAnalysisEventAsync(
        Incident incident,
        IncidentMedia media,
        string message,
        CancellationToken cancellationToken)
    {
        var mediaDto = media.ToDto();
        var statusDto = incident.ToProcessingStatusDto();
        await realtime.PublishAsync(
            new IncidentRealtimeEventDto(
                incident.Id,
                IncidentRealtimeEventTypes.MediaAnalyzed,
                statusDto.IncidentStatus,
                message,
                clock.UtcNow,
                null,
                mediaDto,
                null,
                [],
                statusDto),
            cancellationToken);
    }

    private static bool CanAnalyzeMedia(IncidentMedia media)
    {
        return media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || media.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> SplitLabels(string? labels)
    {
        if (string.IsNullOrWhiteSpace(labels))
        {
            return [];
        }

        return labels
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IncidentSeverity ParseSeverity(string severity)
    {
        if (Enum.TryParse<IncidentSeverity>(severity, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(IncidentSeverity), parsed))
        {
            return parsed;
        }

        throw new ArgumentException("AI analyzer returned an unsupported severity.", nameof(severity));
    }
}
