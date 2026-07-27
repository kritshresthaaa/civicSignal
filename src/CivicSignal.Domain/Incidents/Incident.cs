using System.Security.Cryptography;
using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Incidents;

public sealed class Incident
{
    private const string PublicTrackingCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly List<ProcessingStep> _processingSteps = [];
    private readonly List<IncidentMedia> _mediaItems = [];
    private readonly List<TriagePrediction> _triagePredictions = [];
    private readonly List<DuplicateCandidate> _duplicateCandidates = [];
    private readonly List<IncidentReviewRecord> _reviewRecords = [];
    private readonly List<IncidentUpdateRequest> _updateRequests = [];
    private readonly List<IncidentFeedback> _feedbackItems = [];

    private Incident()
    {
        Description = string.Empty;
        PublicTrackingCode = string.Empty;
    }

    private Incident(string description, GeoPoint location, DateTimeOffset createdAt, string publicTrackingCode)
    {
        Id = Guid.NewGuid();
        Description = description;
        Location = location;
        CreatedAt = createdAt;
        PublicTrackingCode = NormalizePublicTrackingCode(publicTrackingCode);
        Status = IncidentStatus.Submitted;
    }

    public Guid Id { get; private set; }

    public string Description { get; private set; }

    public string PublicTrackingCode { get; private set; }

    public GeoPoint Location { get; private set; }

    public IncidentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ReviewDecision? ReviewDecision { get; private set; }

    public string? ReviewNote { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? CorrectedCategory { get; private set; }

    public string? CorrectedAgencyCode { get; private set; }

    public IncidentSeverity? CorrectedSeverity { get; private set; }

    public Guid? DuplicateOfIncidentId { get; private set; }

    public string? AssignedAgencyCode { get; private set; }

    public string? AssignedTeam { get; private set; }

    public Guid? AssignedByUserId { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public Guid? DispatchedByUserId { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public Guid? DuplicateLinkedByUserId { get; private set; }

    public DateTimeOffset? DuplicateLinkedAt { get; private set; }

    public bool? AcceptedPrediction { get; private set; }

    public bool NotificationAlertsEnabled { get; private set; }

    public string NotificationChannel { get; private set; } = "None";

    public DateTimeOffset? NotificationPreferenceUpdatedAt { get; private set; }

    public IReadOnlyCollection<ProcessingStep> ProcessingSteps => _processingSteps.AsReadOnly();

    public IReadOnlyCollection<IncidentMedia> MediaItems => _mediaItems.AsReadOnly();

    public IReadOnlyCollection<TriagePrediction> TriagePredictions => _triagePredictions.AsReadOnly();

    public IReadOnlyCollection<DuplicateCandidate> DuplicateCandidates => _duplicateCandidates.AsReadOnly();

    public IReadOnlyCollection<IncidentReviewRecord> ReviewRecords => _reviewRecords.AsReadOnly();

    public IReadOnlyCollection<IncidentUpdateRequest> UpdateRequests => _updateRequests.AsReadOnly();

    public IReadOnlyCollection<IncidentFeedback> FeedbackItems => _feedbackItems.AsReadOnly();

    public static Incident Create(
        string description,
        GeoPoint location,
        DateTimeOffset createdAt,
        string? publicTrackingCode = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        return new Incident(
            description.Trim(),
            location,
            createdAt,
            publicTrackingCode ?? GeneratePublicTrackingCode());
    }

    public static string GeneratePublicTrackingCode()
    {
        return string.Concat(
            "CS-",
            RandomNumberGenerator.GetString(PublicTrackingCodeAlphabet, 4),
            "-",
            RandomNumberGenerator.GetString(PublicTrackingCodeAlphabet, 4));
    }

    public static string NormalizePublicTrackingCode(string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            throw new ArgumentException("Public tracking code is required.", nameof(trackingCode));
        }

        var normalized = trackingCode.Trim().ToUpperInvariant();
        if (normalized.Length != 12
            || !normalized.StartsWith("CS-", StringComparison.Ordinal)
            || normalized[7] != '-'
            || normalized[3..7].Any(character => !PublicTrackingCodeAlphabet.Contains(character))
            || normalized[8..].Any(character => !PublicTrackingCodeAlphabet.Contains(character)))
        {
            throw new ArgumentException("Public tracking code must use the CS-XXXX-XXXX format.", nameof(trackingCode));
        }

        return normalized;
    }

    public IncidentReviewRecord Review(
        ReviewDecision decision,
        Guid reviewerUserId,
        string? note,
        DateTimeOffset reviewedAt,
        ValueObjects.IncidentCategory? correctedCategory = null,
        ValueObjects.AgencyCode? correctedAgency = null,
        IncidentSeverity? correctedSeverity = null,
        Guid? duplicateOfIncidentId = null,
        bool? acceptedPrediction = null)
    {
        if (!Enum.IsDefined(typeof(ReviewDecision), decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Review decision is not supported.");
        }

        if (reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer user id is required.", nameof(reviewerUserId));
        }

        if (reviewedAt < CreatedAt)
        {
            throw new ArgumentException("Review date cannot be before incident creation.", nameof(reviewedAt));
        }

        if (Status is IncidentStatus.Approved or IncidentStatus.Rejected or IncidentStatus.Closed or IncidentStatus.Dispatched)
        {
            throw new InvalidOperationException("Incident has already reached a final state.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if ((decision is global::CivicSignal.Domain.Incidents.ReviewDecision.Rejected
                or global::CivicSignal.Domain.Incidents.ReviewDecision.NeedsMoreInfo)
            && trimmedNote is null)
        {
            throw new ArgumentException("A review note is required for this decision.", nameof(note));
        }

        if (correctedSeverity is not null && !Enum.IsDefined(typeof(IncidentSeverity), correctedSeverity.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(correctedSeverity), correctedSeverity, "Severity is not supported.");
        }

        if (duplicateOfIncidentId == Id)
        {
            throw new ArgumentException("An incident cannot be marked as a duplicate of itself.", nameof(duplicateOfIncidentId));
        }

        if (duplicateOfIncidentId == Guid.Empty)
        {
            throw new ArgumentException("Duplicate incident id cannot be empty.", nameof(duplicateOfIncidentId));
        }

        ReviewDecision = decision;
        ReviewNote = trimmedNote;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        CorrectedCategory = correctedCategory?.Value;
        CorrectedAgencyCode = correctedAgency?.Value;
        CorrectedSeverity = correctedSeverity;
        DuplicateOfIncidentId = duplicateOfIncidentId;
        AcceptedPrediction = acceptedPrediction;
        Status = decision switch
        {
            global::CivicSignal.Domain.Incidents.ReviewDecision.Approved => IncidentStatus.Approved,
            global::CivicSignal.Domain.Incidents.ReviewDecision.Rejected => IncidentStatus.Rejected,
            global::CivicSignal.Domain.Incidents.ReviewDecision.NeedsMoreInfo => IncidentStatus.NeedsMoreInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Review decision is not supported.")
        };

        var record = IncidentReviewRecord.Create(
            Id,
            decision,
            reviewerUserId,
            trimmedNote,
            CorrectedCategory,
            CorrectedAgencyCode,
            CorrectedSeverity,
            DuplicateOfIncidentId,
            AcceptedPrediction,
            reviewedAt);
        _reviewRecords.Add(record);

        return record;
    }

    public void Assign(
        string assignedTeam,
        Guid assignedByUserId,
        DateTimeOffset assignedAt,
        ValueObjects.AgencyCode? assignedAgency = null)
    {
        EnsureStaffActionCanChange("Assignment");

        if (assignedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Assigned-by user id is required.", nameof(assignedByUserId));
        }

        if (assignedAt < CreatedAt)
        {
            throw new ArgumentException("Assignment date cannot be before incident creation.", nameof(assignedAt));
        }

        AssignedTeam = NormalizeStaffLabel(assignedTeam, 160, nameof(assignedTeam));
        AssignedAgencyCode = assignedAgency?.Value;
        AssignedByUserId = assignedByUserId;
        AssignedAt = assignedAt;
    }

    public void Dispatch(Guid dispatchedByUserId, DateTimeOffset dispatchedAt)
    {
        EnsureStaffActionCanChange("Dispatch");

        if (dispatchedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Dispatched-by user id is required.", nameof(dispatchedByUserId));
        }

        if (dispatchedAt < CreatedAt)
        {
            throw new ArgumentException("Dispatch date cannot be before incident creation.", nameof(dispatchedAt));
        }

        DispatchedByUserId = dispatchedByUserId;
        DispatchedAt = dispatchedAt;
        Status = IncidentStatus.Dispatched;
    }

    public void LinkDuplicate(Guid duplicateOfIncidentId, Guid linkedByUserId, DateTimeOffset linkedAt)
    {
        EnsureStaffActionCanChange("Duplicate linking");

        if (duplicateOfIncidentId == Guid.Empty)
        {
            throw new ArgumentException("Duplicate incident id cannot be empty.", nameof(duplicateOfIncidentId));
        }

        if (duplicateOfIncidentId == Id)
        {
            throw new ArgumentException("An incident cannot be marked as a duplicate of itself.", nameof(duplicateOfIncidentId));
        }

        if (linkedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Linked-by user id is required.", nameof(linkedByUserId));
        }

        if (linkedAt < CreatedAt)
        {
            throw new ArgumentException("Duplicate link date cannot be before incident creation.", nameof(linkedAt));
        }

        DuplicateOfIncidentId = duplicateOfIncidentId;
        DuplicateLinkedByUserId = linkedByUserId;
        DuplicateLinkedAt = linkedAt;
    }

    public IncidentUpdateRequest RequestUpdate(string message, DateTimeOffset requestedAt)
    {
        EnsureEngagementTimestampIsValid(requestedAt);

        var updateRequest = IncidentUpdateRequest.Create(Id, message, requestedAt);
        _updateRequests.Add(updateRequest);

        return updateRequest;
    }

    public void UpdateNotificationPreference(bool alertsEnabled, string? channel, DateTimeOffset updatedAt)
    {
        EnsureEngagementTimestampIsValid(updatedAt);

        NotificationAlertsEnabled = alertsEnabled;
        NotificationChannel = alertsEnabled
            ? NormalizeNotificationChannel(channel)
            : "None";
        NotificationPreferenceUpdatedAt = updatedAt;
    }

    public IncidentFeedback AddFeedback(int rating, string? comment, DateTimeOffset createdAt)
    {
        EnsureEngagementTimestampIsValid(createdAt);

        var feedback = IncidentFeedback.Create(Id, rating, comment, createdAt);
        _feedbackItems.Add(feedback);

        return feedback;
    }

    public ProcessingStep StartProcessingStep(string name, DateTimeOffset startedAt)
    {
        EnsureProcessingCanChange();

        var step = FindProcessingStep(name);
        if (step is null)
        {
            step = ProcessingStep.Start(Id, name, startedAt);
            _processingSteps.Add(step);
        }
        else
        {
            step.Start(startedAt);
        }

        if (Status is IncidentStatus.Submitted or IncidentStatus.Triaged or IncidentStatus.HumanReviewRequired or IncidentStatus.NeedsMoreInfo)
        {
            Status = IncidentStatus.Processing;
        }

        return step;
    }

    public IncidentMedia AddMedia(
        string fileName,
        string contentType,
        string storageUri,
        DateTimeOffset createdAt)
    {
        EnsureProcessingCanChange();

        var media = IncidentMedia.Create(Id, fileName, contentType, storageUri, createdAt);
        _mediaItems.Add(media);

        return media;
    }

    public TriagePrediction AddTriagePrediction(
        ValueObjects.IncidentCategory category,
        IncidentSeverity severity,
        ValueObjects.ConfidenceScore confidence,
        ValueObjects.AgencyCode suggestedAgency,
        string summary,
        string modelName,
        string? modelVersion,
        string? promptVersion,
        long? processingTimeMilliseconds,
        DateTimeOffset createdAt)
    {
        EnsureProcessingCanChange();

        var prediction = TriagePrediction.Create(
            Id,
            category,
            severity,
            confidence,
            suggestedAgency,
            summary,
            modelName,
            modelVersion,
            promptVersion,
            processingTimeMilliseconds,
            createdAt);

        _triagePredictions.Add(prediction);

        return prediction;
    }

    public DuplicateCandidate AddDuplicateCandidate(
        Guid candidateIncidentId,
        ValueObjects.ConfidenceScore similarityScore,
        string? reason,
        DateTimeOffset createdAt)
    {
        EnsureProcessingCanChange();

        var existingCandidate = _duplicateCandidates.SingleOrDefault(candidate =>
            candidate.CandidateIncidentId == candidateIncidentId);

        if (existingCandidate is not null)
        {
            existingCandidate.Update(similarityScore, reason, createdAt);
            return existingCandidate;
        }

        var duplicateCandidate = DuplicateCandidate.Create(
            Id,
            candidateIncidentId,
            similarityScore,
            reason,
            createdAt);

        _duplicateCandidates.Add(duplicateCandidate);

        return duplicateCandidate;
    }

    public void RequireHumanReview(DateTimeOffset requestedAt)
    {
        EnsureProcessingCanChange();

        if (requestedAt < CreatedAt)
        {
            throw new ArgumentException("Human review request cannot be before incident creation.", nameof(requestedAt));
        }

        if (Status is not IncidentStatus.Approved and not IncidentStatus.Rejected and not IncidentStatus.Closed)
        {
            Status = IncidentStatus.HumanReviewRequired;
        }
    }

    public ProcessingStep CompleteProcessingStep(string name, DateTimeOffset completedAt)
    {
        var step = FindRequiredProcessingStep(name);
        step.Complete(completedAt);

        if (Status is IncidentStatus.Processing && _processingSteps.All(processingStep => processingStep.Status is ProcessingStepStatus.Succeeded))
        {
            Status = IncidentStatus.Triaged;
        }

        return step;
    }

    public ProcessingStep FailProcessingStep(string name, string errorMessage, DateTimeOffset failedAt)
    {
        var step = FindRequiredProcessingStep(name);
        step.Fail(errorMessage, failedAt);

        if (Status is IncidentStatus.Processing)
        {
            Status = IncidentStatus.HumanReviewRequired;
        }

        return step;
    }

    private void EnsureProcessingCanChange()
    {
        if (Status is IncidentStatus.Rejected or IncidentStatus.Closed or IncidentStatus.Dispatched)
        {
            throw new InvalidOperationException("Processing cannot be updated for this incident state.");
        }
    }

    private void EnsureStaffActionCanChange(string actionName)
    {
        if (Status is IncidentStatus.Rejected or IncidentStatus.Closed or IncidentStatus.Dispatched)
        {
            throw new InvalidOperationException($"{actionName} cannot be updated for this incident state.");
        }
    }

    private void EnsureEngagementTimestampIsValid(DateTimeOffset occurredAt)
    {
        if (occurredAt < CreatedAt)
        {
            throw new ArgumentException("Citizen engagement date cannot be before incident creation.", nameof(occurredAt));
        }
    }

    private static string NormalizeNotificationChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Notification channel is required when alerts are enabled.", nameof(channel));
        }

        var normalized = channel.Trim();
        if (normalized.Length > 80)
        {
            throw new ArgumentException("Notification channel cannot exceed 80 characters.", nameof(channel));
        }

        return normalized;
    }

    private static string NormalizeStaffLabel(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Staff assignment label is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Staff assignment label cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private ProcessingStep FindRequiredProcessingStep(string name)
    {
        return FindProcessingStep(name)
            ?? throw new InvalidOperationException("Processing step must be started before it can be completed or failed.");
    }

    private ProcessingStep? FindProcessingStep(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Processing step name is required.", nameof(name));
        }

        var normalized = name.Trim();
        return _processingSteps.SingleOrDefault(step => string.Equals(step.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
