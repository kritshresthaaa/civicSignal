using CivicSignal.Application.Agents;
using CivicSignal.Application.Agents.Models;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using CivicSignal.Worker.Options;
using CivicSignal.Worker.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CivicSignal.Worker.Tests;

public sealed class IncidentProcessingPipelineTests
{
    [Fact]
    public async Task Process_async_runs_configured_steps_in_order()
    {
        var incidentId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var service = new FakeIncidentService();
        var intelligence = new FakeIncidentIntelligenceService();
        var pipeline = BuildPipeline(service, intelligence, "Geocoding", "TriageDraft");

        await pipeline.ProcessAsync(CreateIncident(incidentId, "Submitted"), CancellationToken.None);

        Assert.Equal(
            [
                new ProcessingUpdate(incidentId, "Geocoding", "InProgress", null),
                new ProcessingUpdate(incidentId, "Geocoding", "Succeeded", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "InProgress", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "Succeeded", null)
            ],
            service.Updates);
        Assert.Equal([incidentId], intelligence.AnalyzedIncidentIds);
    }

    [Fact]
    public async Task Process_async_marks_started_step_failed_when_step_throws()
    {
        var incidentId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var service = new FakeIncidentService
        {
            ThrowOnStepName = "MediaAnalysis",
            ThrowOnStatus = "Succeeded"
        };
        var pipeline = BuildPipeline(service, new FakeIncidentIntelligenceService(), "MediaAnalysis");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ProcessAsync(CreateIncident(incidentId, "Submitted"), CancellationToken.None));

        Assert.Equal(
            [
                new ProcessingUpdate(incidentId, "MediaAnalysis", "InProgress", null),
                new ProcessingUpdate(incidentId, "MediaAnalysis", "Succeeded", null),
                new ProcessingUpdate(incidentId, "MediaAnalysis", "Failed", "Simulated worker failure.")
            ],
            service.Updates);
    }

    [Fact]
    public async Task Process_async_analyzes_media_before_triage()
    {
        var incidentId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var service = new FakeIncidentService();
        var mediaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var intelligence = new FakeIncidentIntelligenceService(
            [CreateMedia(incidentId, mediaId, "main-street-pothole.jpg", "image/jpeg")]);
        var pipeline = BuildPipeline(service, intelligence, "MediaAnalysis", "TriageDraft");

        await pipeline.ProcessAsync(CreateIncident(incidentId, "Submitted"), CancellationToken.None);

        Assert.Equal([mediaId], intelligence.AnalyzedMediaIds);
        Assert.Equal([incidentId], intelligence.AnalyzedIncidentIds);
        Assert.Equal(
            [
                new ProcessingUpdate(incidentId, "MediaAnalysis", "InProgress", null),
                new ProcessingUpdate(incidentId, "MediaAnalysis", "Succeeded", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "InProgress", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "Succeeded", null)
            ],
            service.Updates);
    }

    [Fact]
    public async Task Process_async_runs_controlled_agent_workflow_step()
    {
        var incidentId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var service = new FakeIncidentService();
        var agent = new FakeControlledTriageAgentService();
        var pipeline = BuildPipeline(
            service,
            new FakeIncidentIntelligenceService(),
            agent,
            "TriageDraft",
            "ControlledAgentWorkflow");

        await pipeline.ProcessAsync(CreateIncident(incidentId, "Submitted"), CancellationToken.None);

        Assert.Equal([incidentId], agent.WorkflowIncidentIds);
        Assert.Equal(
            [
                new ProcessingUpdate(incidentId, "TriageDraft", "InProgress", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "Succeeded", null),
                new ProcessingUpdate(incidentId, "ControlledAgentWorkflow", "InProgress", null),
                new ProcessingUpdate(incidentId, "ControlledAgentWorkflow", "Succeeded", null)
            ],
            service.Updates);
    }

    [Fact]
    public async Task Process_async_skips_non_submitted_incidents()
    {
        var service = new FakeIncidentService();
        var intelligence = new FakeIncidentIntelligenceService();
        var pipeline = BuildPipeline(service, intelligence, "Geocoding");

        await pipeline.ProcessAsync(
            CreateIncident(Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48"), "Triaged"),
            CancellationToken.None);

        Assert.Empty(service.Updates);
        Assert.Empty(intelligence.AnalyzedIncidentIds);
    }

    [Fact]
    public async Task Process_async_reprocesses_active_incident_for_media_upload_trigger()
    {
        var incidentId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");
        var service = new FakeIncidentService();
        var mediaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var intelligence = new FakeIncidentIntelligenceService(
            [CreateMedia(incidentId, mediaId, "main-street-pothole.jpg", "image/jpeg")]);
        var pipeline = BuildPipeline(service, intelligence, "MediaAnalysis", "TriageDraft");

        await pipeline.ProcessAsync(
            CreateIncident(incidentId, "Triaged"),
            "IncidentMediaUploaded",
            CancellationToken.None);

        Assert.Equal([mediaId], intelligence.AnalyzedMediaIds);
        Assert.Equal([incidentId], intelligence.AnalyzedIncidentIds);
        Assert.Equal(
            [
                new ProcessingUpdate(incidentId, "MediaAnalysis", "InProgress", null),
                new ProcessingUpdate(incidentId, "MediaAnalysis", "Succeeded", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "InProgress", null),
                new ProcessingUpdate(incidentId, "TriageDraft", "Succeeded", null)
            ],
            service.Updates);
    }

    private static IncidentProcessingPipeline BuildPipeline(
        FakeIncidentService service,
        FakeIncidentIntelligenceService intelligence,
        params string[] steps)
    {
        return BuildPipeline(service, intelligence, new FakeControlledTriageAgentService(), steps);
    }

    private static IncidentProcessingPipeline BuildPipeline(
        FakeIncidentService service,
        FakeIncidentIntelligenceService intelligence,
        FakeControlledTriageAgentService agent,
        params string[] steps)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new IncidentProcessingWorkerOptions
        {
            StepDelayMilliseconds = 0,
            Steps = steps
        });

        return new IncidentProcessingPipeline(
            service,
            intelligence,
            agent,
            options,
            NullLogger<IncidentProcessingPipeline>.Instance);
    }

    private static IncidentDto CreateIncident(Guid incidentId, string status)
    {
        return new IncidentDto(
            incidentId,
            "CS-ABCD-2345",
            "Large pothole near Main Street",
            40.7128,
            -74.0060,
            status,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static IncidentMediaDto CreateMedia(
        Guid incidentId,
        Guid mediaId,
        string fileName,
        string contentType)
    {
        return new IncidentMediaDto(
            mediaId,
            incidentId,
            fileName,
            contentType,
            $"/media/{fileName}",
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : "Audio",
            "Pending",
            null,
            null,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
    }

    private sealed class FakeIncidentService : IIncidentService
    {
        private readonly List<ProcessingUpdate> _updates = [];

        public IReadOnlyCollection<ProcessingUpdate> Updates => _updates;

        public string? ThrowOnStepName { get; init; }

        public string? ThrowOnStatus { get; init; }

        public Task<IncidentDto> CreateAsync(CreateIncidentInput input, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<IncidentDto>> SearchAsync(
            IncidentSearchInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto> ReviewAsync(
            Guid incidentId,
            ReviewIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto> AssignAsync(
            Guid incidentId,
            AssignIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto> DispatchAsync(
            Guid incidentId,
            DispatchIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentDto> LinkDuplicateAsync(
            Guid incidentId,
            LinkDuplicateIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<IncidentReviewDto>?> GetReviewHistoryAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentProcessingStatusDto?> GetProcessingStatusAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentProcessingStatusDto> UpdateProcessingStatusAsync(
            Guid incidentId,
            UpdateProcessingStatusInput input,
            CancellationToken cancellationToken = default)
        {
            _updates.Add(new ProcessingUpdate(incidentId, input.StepName, input.Status, input.ErrorMessage));

            if (string.Equals(input.StepName, ThrowOnStepName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(input.Status, ThrowOnStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Simulated worker failure.");
            }

            return Task.FromResult(new IncidentProcessingStatusDto(incidentId, input.Status, []));
        }

        public Task<IncidentUpdateRequestDto> RequestUpdateAsync(
            Guid incidentId,
            CreateIncidentUpdateRequestInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentNotificationPreferenceDto> UpdateNotificationPreferenceAsync(
            Guid incidentId,
            UpdateNotificationPreferenceInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<IncidentFeedbackDto>?> GetFeedbackAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IncidentFeedbackDto> AddFeedbackAsync(
            Guid incidentId,
            CreateIncidentFeedbackInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeIncidentIntelligenceService(
        IReadOnlyCollection<IncidentMediaDto>? mediaItems = null) : IIncidentIntelligenceService
    {
        private readonly List<Guid> _analyzedIncidentIds = [];
        private readonly List<Guid> _analyzedMediaIds = [];

        public IReadOnlyCollection<Guid> AnalyzedIncidentIds => _analyzedIncidentIds;

        public IReadOnlyCollection<Guid> AnalyzedMediaIds => _analyzedMediaIds;

        public Task<IncidentMediaDto> AddMediaAsync(
            Guid incidentId,
            AddIncidentMediaInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<IncidentMediaDto>?> GetMediaAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<IncidentMediaDto>?>(mediaItems);
        }

        public Task<IncidentMediaDto> AnalyzeMediaAsync(
            Guid incidentId,
            Guid mediaId,
            CancellationToken cancellationToken = default)
        {
            _analyzedMediaIds.Add(mediaId);

            var media = mediaItems?.Single(item => item.Id == mediaId)
                ?? throw new InvalidOperationException("Media item was not configured.");

            return Task.FromResult(media);
        }

        public Task<TriagePredictionDto> AnalyzeAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            _analyzedIncidentIds.Add(incidentId);

            return Task.FromResult(new TriagePredictionDto(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                incidentId,
                "RoadDamage",
                "High",
                0.91,
                "High RoadDamage report routed to DOT.",
                "DOT",
                "test-analyzer",
                "1.0",
                "test-prompt-v1",
                42,
                DateTimeOffset.Parse("2026-07-23T12:03:00Z"),
                []));
        }

        public Task<TriagePredictionDto?> GetLatestPredictionAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<DuplicateCandidateDto>?> GetDuplicateCandidatesAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeControlledTriageAgentService : IControlledTriageAgentService
    {
        private readonly List<Guid> _workflowIncidentIds = [];

        public IReadOnlyCollection<Guid> WorkflowIncidentIds => _workflowIncidentIds;

        public Task<ControlledTriageWorkflowDto> RunAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            _workflowIncidentIds.Add(incidentId);

            return Task.FromResult(new ControlledTriageWorkflowDto(
                incidentId,
                "draft_work_order_ready",
                false,
                null,
                0.72,
                null,
                new DraftWorkOrderDto(
                    "RoadDamage response",
                    "DOT",
                    "High",
                    "Draft prepared.",
                    []),
                []));
        }
    }

    private sealed record ProcessingUpdate(Guid IncidentId, string StepName, string Status, string? ErrorMessage);
}
