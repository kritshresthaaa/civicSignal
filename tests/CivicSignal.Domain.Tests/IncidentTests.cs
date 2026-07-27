using CivicSignal.Domain.Incidents;
using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Tests;

public sealed class IncidentTests
{
    [Fact]
    public void Create_sets_initial_submitted_state()
    {
        var createdAt = DateTimeOffset.Parse("2026-07-23T12:00:00Z");

        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            createdAt);

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal("Large pothole near Main Street", incident.Description);
        Assert.Equal(IncidentStatus.Submitted, incident.Status);
        Assert.Equal(createdAt, incident.CreatedAt);
    }

    [Fact]
    public void Create_assigns_public_tracking_code()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.Matches("^CS-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", incident.PublicTrackingCode);
    }

    [Fact]
    public void Create_normalizes_supplied_public_tracking_code()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
            " cs-abcd-2345 ");

        Assert.Equal("CS-ABCD-2345", incident.PublicTrackingCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCD-2345")]
    [InlineData("CS-ABC1-2345")]
    [InlineData("CS-ABCD-0123")]
    public void Create_rejects_invalid_public_tracking_code(string trackingCode)
    {
        Assert.Throws<ArgumentException>(() =>
            Incident.Create(
                "Large pothole near Main Street",
                new GeoPoint(40.7128, -74.0060),
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                trackingCode));
    }

    [Fact]
    public void Create_rejects_empty_description()
    {
        Assert.Throws<ArgumentException>(() =>
            Incident.Create(" ", new GeoPoint(40.7128, -74.0060), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Review_approved_updates_review_state()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var reviewerId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var reviewedAt = DateTimeOffset.Parse("2026-07-23T12:10:00Z");

        var duplicateOfIncidentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var review = incident.Review(
            ReviewDecision.Approved,
            reviewerId,
            "Confirmed by city reviewer.",
            reviewedAt,
            new IncidentCategory("RoadDamage"),
            new AgencyCode("dot"),
            IncidentSeverity.High,
            duplicateOfIncidentId,
            acceptedPrediction: false);

        Assert.Equal(IncidentStatus.Approved, incident.Status);
        Assert.Equal(ReviewDecision.Approved, incident.ReviewDecision);
        Assert.Equal("Confirmed by city reviewer.", incident.ReviewNote);
        Assert.Equal(reviewerId, incident.ReviewedByUserId);
        Assert.Equal(reviewedAt, incident.ReviewedAt);
        Assert.Equal("RoadDamage", incident.CorrectedCategory);
        Assert.Equal("DOT", incident.CorrectedAgencyCode);
        Assert.Equal(IncidentSeverity.High, incident.CorrectedSeverity);
        Assert.Equal(duplicateOfIncidentId, incident.DuplicateOfIncidentId);
        Assert.False(incident.AcceptedPrediction);
        Assert.Equal(review.Id, Assert.Single(incident.ReviewRecords).Id);
        Assert.Equal("RoadDamage", review.CorrectedCategory);
    }

    [Fact]
    public void Review_rejected_requires_note()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.Throws<ArgumentException>(() =>
            incident.Review(
                ReviewDecision.Rejected,
                Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48"),
                " ",
                DateTimeOffset.Parse("2026-07-23T12:10:00Z")));
    }

    [Fact]
    public void Review_final_incident_rejects_second_review()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var reviewerId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");

        incident.Review(ReviewDecision.Approved, reviewerId, null, DateTimeOffset.Parse("2026-07-23T12:10:00Z"));

        Assert.Throws<InvalidOperationException>(() =>
            incident.Review(ReviewDecision.Rejected, reviewerId, "Wrong category.", DateTimeOffset.Parse("2026-07-23T12:12:00Z")));
    }

    [Fact]
    public void Assign_records_staff_assignment_metadata()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var operatorId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var assignedAt = DateTimeOffset.Parse("2026-07-23T12:08:00Z");

        incident.Assign("DOT intake queue", operatorId, assignedAt, new AgencyCode("dot"));

        Assert.Equal("DOT intake queue", incident.AssignedTeam);
        Assert.Equal("DOT", incident.AssignedAgencyCode);
        Assert.Equal(operatorId, incident.AssignedByUserId);
        Assert.Equal(assignedAt, incident.AssignedAt);
        Assert.Equal(IncidentStatus.Submitted, incident.Status);
    }

    [Fact]
    public void Dispatch_updates_incident_status()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var operatorId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var dispatchedAt = DateTimeOffset.Parse("2026-07-23T12:15:00Z");

        incident.Dispatch(operatorId, dispatchedAt);

        Assert.Equal(IncidentStatus.Dispatched, incident.Status);
        Assert.Equal(operatorId, incident.DispatchedByUserId);
        Assert.Equal(dispatchedAt, incident.DispatchedAt);
    }

    [Fact]
    public void Link_duplicate_records_merge_metadata()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var duplicateOfIncidentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var operatorId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var linkedAt = DateTimeOffset.Parse("2026-07-23T12:20:00Z");

        incident.LinkDuplicate(duplicateOfIncidentId, operatorId, linkedAt);

        Assert.Equal(duplicateOfIncidentId, incident.DuplicateOfIncidentId);
        Assert.Equal(operatorId, incident.DuplicateLinkedByUserId);
        Assert.Equal(linkedAt, incident.DuplicateLinkedAt);
    }

    [Fact]
    public void Link_duplicate_rejects_self_link()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.Throws<ArgumentException>(() =>
            incident.LinkDuplicate(
                incident.Id,
                Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48"),
                DateTimeOffset.Parse("2026-07-23T12:20:00Z")));
    }

    [Fact]
    public void Processing_step_can_start_and_complete()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        incident.StartProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:01:00Z"));
        incident.CompleteProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:02:00Z"));

        var step = Assert.Single(incident.ProcessingSteps);
        Assert.Equal("MediaAnalysis", step.Name);
        Assert.Equal(ProcessingStepStatus.Succeeded, step.Status);
        Assert.Equal(IncidentStatus.Triaged, incident.Status);
    }

    [Fact]
    public void Starting_next_processing_step_moves_triaged_incident_back_to_processing()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        incident.StartProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:01:00Z"));
        incident.CompleteProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:02:00Z"));

        incident.StartProcessingStep("DuplicateCheck", DateTimeOffset.Parse("2026-07-23T12:03:00Z"));

        Assert.Equal(IncidentStatus.Processing, incident.Status);
        Assert.Equal(2, incident.ProcessingSteps.Count);
    }

    [Fact]
    public void Succeeded_processing_step_can_restart_for_new_evidence()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        incident.StartProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:01:00Z"));
        incident.CompleteProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:02:00Z"));

        incident.StartProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:05:00Z"));

        var step = Assert.Single(incident.ProcessingSteps);
        Assert.Equal(ProcessingStepStatus.InProgress, step.Status);
        Assert.Null(step.CompletedAt);
        Assert.Equal(IncidentStatus.Processing, incident.Status);
    }

    [Fact]
    public void Require_human_review_updates_active_incident_status()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        incident.RequireHumanReview(DateTimeOffset.Parse("2026-07-23T12:03:00Z"));

        Assert.Equal(IncidentStatus.HumanReviewRequired, incident.Status);
    }

    [Fact]
    public void Processing_step_failure_requires_error_message()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        incident.StartProcessingStep("DuplicateCheck", DateTimeOffset.Parse("2026-07-23T12:01:00Z"));

        Assert.Throws<ArgumentException>(() =>
            incident.FailProcessingStep("DuplicateCheck", " ", DateTimeOffset.Parse("2026-07-23T12:02:00Z")));
    }

    [Fact]
    public void Processing_step_must_start_before_completion()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.Throws<InvalidOperationException>(() =>
            incident.CompleteProcessingStep("MediaAnalysis", DateTimeOffset.Parse("2026-07-23T12:02:00Z")));
    }

    [Fact]
    public void GeoPoint_rejects_invalid_coordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(91, -74.0060));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(40.7128, -181));
    }

    [Fact]
    public void Add_media_prediction_and_duplicate_candidate_tracks_ai_outputs()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        var duplicateIncidentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var media = incident.AddMedia(
            "main-street-pothole.jpg",
            "image/jpeg",
            "placeholder://incident-media/main-street-pothole.jpg",
            DateTimeOffset.Parse("2026-07-23T12:01:00Z"));
        var prediction = incident.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.91),
            new AgencyCode("dot"),
            "High RoadDamage report routed to DOT.",
            "test-analyzer",
            "1.0",
            "test-prompt-v1",
            125,
            DateTimeOffset.Parse("2026-07-23T12:02:00Z"));
        prediction.AddEvidence(
            "Text",
            "Category keyword match",
            "Matched category term(s): pothole.",
            0.91,
            DateTimeOffset.Parse("2026-07-23T12:02:01Z"));
        var duplicate = incident.AddDuplicateCandidate(
            duplicateIncidentId,
            new ConfidenceScore(0.88),
            "Similar report text near the same coordinates.",
            DateTimeOffset.Parse("2026-07-23T12:03:00Z"));

        Assert.Equal(IncidentMediaType.Image, media.MediaType);
        Assert.Equal("RoadDamage", prediction.Category.Value);
        Assert.Equal("DOT", prediction.SuggestedAgency.Value);
        Assert.Equal("1.0", prediction.ModelVersion);
        Assert.Equal("test-prompt-v1", prediction.PromptVersion);
        Assert.Equal(125, prediction.ProcessingTimeMilliseconds);
        Assert.Equal("Text", Assert.Single(prediction.EvidenceItems).Kind);
        Assert.Equal(duplicateIncidentId, duplicate.CandidateIncidentId);
        Assert.Equal(0.88, duplicate.SimilarityScore.Value);
    }

    [Fact]
    public void ConfidenceScore_rejects_invalid_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfidenceScore(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfidenceScore(1.01));
    }

    [Fact]
    public void Citizen_engagement_tracks_update_preference_and_feedback()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        var updateRequest = incident.RequestUpdate(
            "Could you notify me when a crew is assigned?",
            DateTimeOffset.Parse("2026-07-23T12:05:00Z"));
        incident.UpdateNotificationPreference(
            alertsEnabled: true,
            channel: "Browser",
            DateTimeOffset.Parse("2026-07-23T12:06:00Z"));
        var feedback = incident.AddFeedback(
            5,
            "The status page was clear.",
            DateTimeOffset.Parse("2026-07-23T12:07:00Z"));

        Assert.Equal("Could you notify me when a crew is assigned?", updateRequest.Message);
        Assert.Equal(IncidentUpdateRequestStatus.Open, updateRequest.Status);
        Assert.Equal(updateRequest.Id, Assert.Single(incident.UpdateRequests).Id);
        Assert.True(incident.NotificationAlertsEnabled);
        Assert.Equal("Browser", incident.NotificationChannel);
        Assert.Equal(DateTimeOffset.Parse("2026-07-23T12:06:00Z"), incident.NotificationPreferenceUpdatedAt);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal(feedback.Id, Assert.Single(incident.FeedbackItems).Id);
    }

    [Fact]
    public void Citizen_engagement_rejects_invalid_values()
    {
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.Throws<ArgumentException>(() =>
            incident.RequestUpdate(" ", DateTimeOffset.Parse("2026-07-23T12:05:00Z")));
        Assert.Throws<ArgumentException>(() =>
            incident.UpdateNotificationPreference(true, " ", DateTimeOffset.Parse("2026-07-23T12:06:00Z")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            incident.AddFeedback(6, null, DateTimeOffset.Parse("2026-07-23T12:07:00Z")));
    }
}
