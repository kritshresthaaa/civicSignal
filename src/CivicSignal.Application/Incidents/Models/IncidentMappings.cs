using CivicSignal.Domain.Incidents;

namespace CivicSignal.Application.Incidents.Models;

public static class IncidentMappings
{
    public static IncidentDto ToDto(this Incident incident)
    {
        return new IncidentDto(
            incident.Id,
            incident.PublicTrackingCode,
            incident.Description,
            incident.Location.Latitude,
            incident.Location.Longitude,
            incident.Status.ToString(),
            incident.CreatedAt,
            incident.ReviewDecision?.ToString(),
            incident.ReviewNote,
            incident.ReviewedByUserId,
            incident.ReviewedAt,
            incident.CorrectedCategory,
            incident.CorrectedAgencyCode,
            incident.CorrectedSeverity?.ToString(),
            incident.DuplicateOfIncidentId,
            incident.AcceptedPrediction,
            incident.NotificationAlertsEnabled,
            incident.NotificationChannel,
            incident.NotificationPreferenceUpdatedAt,
            incident.AssignedAgencyCode,
            incident.AssignedTeam,
            incident.AssignedByUserId,
            incident.AssignedAt,
            incident.DispatchedByUserId,
            incident.DispatchedAt,
            incident.DuplicateLinkedByUserId,
            incident.DuplicateLinkedAt);
    }

    public static IncidentProcessingStatusDto ToProcessingStatusDto(this Incident incident)
    {
        var steps = incident.ProcessingSteps
            .OrderBy(step => step.StartedAt ?? step.UpdatedAt)
            .ThenBy(step => step.Name)
            .Select(step => new ProcessingStepDto(
                step.Id,
                step.Name,
                step.Status.ToString(),
                step.StartedAt,
                step.CompletedAt,
                step.ErrorMessage,
                step.UpdatedAt))
            .ToArray();

        return new IncidentProcessingStatusDto(incident.Id, incident.Status.ToString(), steps);
    }

    public static IncidentMediaDto ToDto(this IncidentMedia media)
    {
        return new IncidentMediaDto(
            media.Id,
            media.IncidentId,
            media.FileName,
            media.ContentType,
            media.StorageUri,
            media.MediaType.ToString(),
            media.AnalysisStatus.ToString(),
            media.AnalysisSummary,
            media.Transcript,
            SplitLabels(media.DetectedLabels),
            media.AnalysisConfidence,
            media.AnalysisModelName,
            media.AnalysisModelVersion,
            media.AnalysisProcessingTimeMilliseconds,
            media.AnalysisError,
            media.AnalyzedAt,
            media.CreatedAt);
    }

    public static TriagePredictionDto ToDto(this TriagePrediction prediction)
    {
        return new TriagePredictionDto(
            prediction.Id,
            prediction.IncidentId,
            prediction.Category.Value,
            prediction.Severity.ToString(),
            prediction.Confidence.Value,
            prediction.Summary,
            prediction.SuggestedAgency.Value,
            prediction.ModelName,
            prediction.ModelVersion,
            prediction.PromptVersion,
            prediction.ProcessingTimeMilliseconds,
            prediction.CreatedAt,
            prediction.EvidenceItems
                .OrderBy(evidence => evidence.CreatedAt)
                .Select(evidence => evidence.ToDto())
                .ToArray());
    }

    public static PredictionEvidenceDto ToDto(this PredictionEvidence evidence)
    {
        return new PredictionEvidenceDto(
            evidence.Id,
            evidence.TriagePredictionId,
            evidence.Kind,
            evidence.Title,
            evidence.Detail,
            evidence.Confidence,
            evidence.CreatedAt);
    }

    public static DuplicateCandidateDto ToDto(this DuplicateCandidate candidate)
    {
        return new DuplicateCandidateDto(
            candidate.Id,
            candidate.IncidentId,
            candidate.CandidateIncidentId,
            candidate.SimilarityScore.Value,
            candidate.Reason,
            candidate.CreatedAt,
            candidate.UpdatedAt);
    }

    public static IncidentReviewDto ToDto(this IncidentReviewRecord review)
    {
        return new IncidentReviewDto(
            review.Id,
            review.IncidentId,
            review.Decision.ToString(),
            review.Note,
            review.ReviewerUserId,
            review.CorrectedCategory,
            review.CorrectedAgencyCode,
            review.CorrectedSeverity?.ToString(),
            review.DuplicateOfIncidentId,
            review.AcceptedPrediction,
            review.CreatedAt);
    }

    public static IncidentUpdateRequestDto ToDto(this IncidentUpdateRequest updateRequest)
    {
        return new IncidentUpdateRequestDto(
            updateRequest.Id,
            updateRequest.IncidentId,
            updateRequest.Message,
            updateRequest.Status.ToString(),
            updateRequest.CreatedAt);
    }

    public static IncidentNotificationPreferenceDto ToNotificationPreferenceDto(this Incident incident)
    {
        return new IncidentNotificationPreferenceDto(
            incident.Id,
            incident.NotificationAlertsEnabled,
            incident.NotificationChannel,
            incident.NotificationPreferenceUpdatedAt ?? incident.CreatedAt);
    }

    public static IncidentFeedbackDto ToDto(this IncidentFeedback feedback)
    {
        return new IncidentFeedbackDto(
            feedback.Id,
            feedback.IncidentId,
            feedback.Rating,
            feedback.Comment,
            feedback.CreatedAt);
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
}
