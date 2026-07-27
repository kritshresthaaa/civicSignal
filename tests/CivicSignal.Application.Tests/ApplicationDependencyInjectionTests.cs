using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Application.Abstractions.Geocoding;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Realtime;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Agents;
using CivicSignal.Application.AiEvaluations;
using CivicSignal.Application.Common;
using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.Forecasting;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.ModelLab;
using CivicSignal.Domain.Incidents.ValueObjects;
using CivicSignal.Domain.Incidents;
using Microsoft.Extensions.DependencyInjection;

namespace CivicSignal.Application.Tests;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_application_services()
    {
        using var provider = BuildProvider(new FakeIncidentRepository());
        Assert.NotNull(provider.GetRequiredService<IIncidentService>());
        Assert.NotNull(provider.GetRequiredService<IIncidentIntelligenceService>());
        Assert.NotNull(provider.GetRequiredService<IControlledTriageAgentService>());
        Assert.NotNull(provider.GetRequiredService<IAiEvaluationService>());
        Assert.NotNull(provider.GetRequiredService<IIncidentForecastingService>());
        Assert.NotNull(provider.GetRequiredService<IModelLabService>());
        Assert.NotNull(provider.GetRequiredService<IIncidentProcessingQueue>());
        Assert.NotNull(provider.GetRequiredService<IGeocodingService>());
        Assert.NotNull(provider.GetRequiredService<IWeatherService>());
    }

    [Fact]
    public async Task Ai_evaluation_service_returns_passed_baseline_gates()
    {
        using var provider = BuildProvider(new FakeIncidentRepository());
        var evaluations = provider.GetRequiredService<IAiEvaluationService>();

        var report = await evaluations.GetBaselineReportAsync(CancellationToken.None);

        Assert.Equal("CivicSignal deterministic baseline", report.BaselineName);
        Assert.Contains(report.MetricGroups, group => group.Name == "Images");
        Assert.Contains(report.MetricGroups, group => group.Name == "Audio");
        Assert.Contains(report.MetricGroups, group => group.Name == "Generated Reports");
        Assert.All(report.Gates, gate => Assert.True(gate.Passed));
        Assert.Contains(report.ModelRuns, run => run.Provider == "Hugging Face" && run.Status == "Not connected");
    }

    [Fact]
    public async Task Model_lab_analysis_exposes_token_embedding_and_softmax_steps()
    {
        using var provider = BuildProvider(new FakeIncidentRepository());
        var modelLab = provider.GetRequiredService<IModelLabService>();

        var analysis = await modelLab.AnalyzeAsync(
            new ModelLabAnalysisInput("Large pothole on Pine Street forcing cars to swerve.", 16),
            CancellationToken.None);

        Assert.Equal("RoadDamage", analysis.PredictedCategory);
        Assert.Equal("DOT", analysis.SuggestedAgencyCode);
        Assert.Equal(16, analysis.EmbeddingPreview.Count);
        Assert.Contains(analysis.Tokens, token => token.Normalized == "pothole" && !token.IsStopWord);
        Assert.Contains(analysis.ClassScores, score => score.Category == "RoadDamage" && score.Probability > 0.5);
        Assert.Equal(1, analysis.ClassScores.Sum(score => score.Probability), precision: 2);
    }

    [Fact]
    public async Task Create_then_get_incident_through_service()
    {
        var repository = new FakeIncidentRepository();
        using var provider = BuildProvider(repository);

        var incidents = provider.GetRequiredService<IIncidentService>();

        var created = await incidents.CreateAsync(
            new CreateIncidentInput("Large pothole near Main Street", 40.7128, -74.0060),
            CancellationToken.None);

        var loaded = await incidents.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal("Submitted", loaded.Status);
        Assert.Equal(1024, repository.GetTextEmbeddingLength(created.Id));
    }

    [Fact]
    public async Task Create_incident_enqueues_processing_message()
    {
        var repository = new FakeIncidentRepository();
        var queue = new RecordingIncidentProcessingQueue();
        using var provider = BuildProvider(repository, processingQueue: queue);

        var incidents = provider.GetRequiredService<IIncidentService>();

        var created = await incidents.CreateAsync(
            new CreateIncidentInput("Large pothole near Main Street", 40.7128, -74.0060),
            CancellationToken.None);

        var queued = Assert.Single(queue.Messages);
        Assert.Equal(created.Id, queued.IncidentId);
        Assert.Equal("IncidentCreated", queued.Trigger);
    }

    [Fact]
    public async Task Search_incidents_clamps_page_size()
    {
        var repository = new FakeIncidentRepository();
        using var provider = BuildProvider(repository);
        var incidents = provider.GetRequiredService<IIncidentService>();

        for (var index = 0; index < 3; index++)
        {
            await incidents.CreateAsync(
                new CreateIncidentInput($"Pothole report {index}", 40 + index, -74),
                CancellationToken.None);
        }

        var results = await incidents.SearchAsync(
            new IncidentSearchInput(Status: "Submitted", Page: 1, PageSize: 2),
            CancellationToken.None);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Review_incident_updates_review_state()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository);
        var incidents = provider.GetRequiredService<IIncidentService>();
        var reviewerId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");

        var reviewed = await incidents.ReviewAsync(
            incident.Id,
            new ReviewIncidentInput(
                "Approved",
                "Confirmed by reviewer.",
                reviewerId,
                CorrectedCategory: "RoadDamage",
                CorrectedAgencyCode: "dot",
                CorrectedSeverity: "High",
                DuplicateOfIncidentId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                AcceptedPrediction: false),
            CancellationToken.None);
        var history = await incidents.GetReviewHistoryAsync(incident.Id, CancellationToken.None);

        Assert.Equal("Approved", reviewed.Status);
        Assert.Equal("Approved", reviewed.ReviewDecision);
        Assert.Equal("Confirmed by reviewer.", reviewed.ReviewNote);
        Assert.Equal(reviewerId, reviewed.ReviewedByUserId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-23T12:00:00Z"), reviewed.ReviewedAt);
        Assert.Equal("RoadDamage", reviewed.CorrectedCategory);
        Assert.Equal("DOT", reviewed.CorrectedAgencyCode);
        Assert.Equal("High", reviewed.CorrectedSeverity);
        Assert.False(reviewed.AcceptedPrediction);
        Assert.NotNull(history);
        Assert.Equal("RoadDamage", Assert.Single(history).CorrectedCategory);
    }

    [Fact]
    public async Task Review_missing_incident_throws_not_found()
    {
        using var provider = BuildProvider(new FakeIncidentRepository());
        var incidents = provider.GetRequiredService<IIncidentService>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            incidents.ReviewAsync(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                new ReviewIncidentInput("Approved", null, Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48")),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_processing_status_tracks_steps()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository);
        var incidents = provider.GetRequiredService<IIncidentService>();

        var inProgress = await incidents.UpdateProcessingStatusAsync(
            incident.Id,
            new UpdateProcessingStatusInput("MediaAnalysis", "InProgress", null),
            CancellationToken.None);

        Assert.Equal("Processing", inProgress.IncidentStatus);
        Assert.Equal("InProgress", Assert.Single(inProgress.Steps).Status);

        var completed = await incidents.UpdateProcessingStatusAsync(
            incident.Id,
            new UpdateProcessingStatusInput("MediaAnalysis", "Succeeded", null),
            CancellationToken.None);

        var step = Assert.Single(completed.Steps);
        Assert.Equal("Triaged", completed.IncidentStatus);
        Assert.Equal("Succeeded", step.Status);
        Assert.NotNull(step.CompletedAt);
    }

    [Fact]
    public async Task Update_processing_status_publishes_realtime_event()
    {
        var repository = new FakeIncidentRepository();
        var notifier = new RecordingIncidentRealtimeNotifier();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, realtime: notifier);
        var incidents = provider.GetRequiredService<IIncidentService>();

        await incidents.UpdateProcessingStatusAsync(
            incident.Id,
            new UpdateProcessingStatusInput("DuplicateCheck", "InProgress", null),
            CancellationToken.None);

        var incidentEvent = Assert.Single(notifier.Events);
        Assert.Equal(IncidentRealtimeEventTypes.ProcessingStatusChanged, incidentEvent.EventType);
        Assert.Equal(incident.Id, incidentEvent.IncidentId);
        Assert.Equal("Processing", incidentEvent.IncidentStatus);
        Assert.Equal("DuplicateCheck", Assert.Single(incidentEvent.ProcessingStatus.Steps).Name);
    }

    [Fact]
    public async Task Get_processing_status_returns_steps()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        incident.StartProcessingStep("DuplicateCheck", DateTimeOffset.Parse("2026-07-23T11:55:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository);
        var incidents = provider.GetRequiredService<IIncidentService>();

        var status = await incidents.GetProcessingStatusAsync(incident.Id, CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("Processing", status.IncidentStatus);
        Assert.Equal("DuplicateCheck", Assert.Single(status.Steps).Name);
    }

    [Fact]
    public async Task Citizen_engagement_methods_persist_and_publish_realtime_events()
    {
        var repository = new FakeIncidentRepository();
        var notifier = new RecordingIncidentRealtimeNotifier();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, realtime: notifier);
        var incidents = provider.GetRequiredService<IIncidentService>();

        var updateRequest = await incidents.RequestUpdateAsync(
            incident.Id,
            new CreateIncidentUpdateRequestInput("Could you notify me when a crew is assigned?"),
            CancellationToken.None);
        var preference = await incidents.UpdateNotificationPreferenceAsync(
            incident.Id,
            new UpdateNotificationPreferenceInput(true, "Browser"),
            CancellationToken.None);
        var feedback = await incidents.AddFeedbackAsync(
            incident.Id,
            new CreateIncidentFeedbackInput(5, "The status page was clear."),
            CancellationToken.None);

        Assert.Equal("Could you notify me when a crew is assigned?", updateRequest.Message);
        Assert.Equal("Open", updateRequest.Status);
        Assert.True(preference.AlertsEnabled);
        Assert.Equal("Browser", preference.Channel);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal(
            [
                IncidentRealtimeEventTypes.UpdateRequested,
                IncidentRealtimeEventTypes.NotificationPreferenceUpdated,
                IncidentRealtimeEventTypes.FeedbackReceived
            ],
            notifier.Events.Select(incidentEvent => incidentEvent.EventType));
    }

    [Fact]
    public async Task Add_media_attaches_incident_media()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository);
        var intelligence = provider.GetRequiredService<IIncidentIntelligenceService>();

        var media = await intelligence.AddMediaAsync(
            incident.Id,
            new AddIncidentMediaInput(
                "main-street-pothole.jpg",
                "image/jpeg",
                "placeholder://incident-media/main-street-pothole.jpg"),
            CancellationToken.None);

        Assert.Equal(incident.Id, media.IncidentId);
        Assert.Equal("Image", media.MediaType);
        Assert.Equal("Pending", media.AnalysisStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-07-23T12:00:00Z"), media.CreatedAt);
    }

    [Fact]
    public async Task Add_media_enqueues_incident_processing_message()
    {
        var repository = new FakeIncidentRepository();
        var queue = new RecordingIncidentProcessingQueue();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, processingQueue: queue);
        var intelligence = provider.GetRequiredService<IIncidentIntelligenceService>();

        await intelligence.AddMediaAsync(
            incident.Id,
            new AddIncidentMediaInput(
                "main-street-pothole.jpg",
                "image/jpeg",
                "placeholder://incident-media/main-street-pothole.jpg"),
            CancellationToken.None);

        var queued = Assert.Single(queue.Messages);
        Assert.Equal(incident.Id, queued.IncidentId);
        Assert.Equal("IncidentMediaUploaded", queued.Trigger);
    }

    [Fact]
    public async Task Analyze_media_stores_ai_media_result_and_publishes_realtime_events()
    {
        var repository = new FakeIncidentRepository();
        var notifier = new RecordingIncidentRealtimeNotifier();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, realtime: notifier);
        var intelligence = provider.GetRequiredService<IIncidentIntelligenceService>();
        var media = await intelligence.AddMediaAsync(
            incident.Id,
            new AddIncidentMediaInput(
                "main-street-pothole.jpg",
                "image/jpeg",
                "placeholder://incident-media/main-street-pothole.jpg"),
            CancellationToken.None);

        var analyzed = await intelligence.AnalyzeMediaAsync(incident.Id, media.Id, CancellationToken.None);

        Assert.Equal("Succeeded", analyzed.AnalysisStatus);
        Assert.Equal("Image evidence suggests road damage.", analyzed.AnalysisSummary);
        Assert.Contains("road damage", analyzed.DetectedLabels);
        Assert.Equal(0.77, analyzed.AnalysisConfidence);
        Assert.Equal("test-media-analyzer", analyzed.AnalysisModelName);
        Assert.Contains(notifier.Events, incidentEvent => incidentEvent.EventType == IncidentRealtimeEventTypes.MediaAnalyzed);
        Assert.Equal("Succeeded", notifier.Events.Last().Media?.AnalysisStatus);
    }

    [Fact]
    public async Task Analyze_incident_saves_prediction_and_duplicate_candidates()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        var duplicate = Incident.Create(
            "Large pothole near Main Street crosswalk",
            new GeoPoint(40.7129, -74.0061),
            DateTimeOffset.Parse("2026-07-23T11:51:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);
        await repository.AddAsync(duplicate, CancellationToken.None);

        using var provider = BuildProvider(repository, new FakeDuplicateIncidentSearchService(duplicate.Id));
        var intelligence = provider.GetRequiredService<IIncidentIntelligenceService>();

        var prediction = await intelligence.AnalyzeAsync(incident.Id, CancellationToken.None);
        var candidates = await intelligence.GetDuplicateCandidatesAsync(incident.Id, CancellationToken.None);

        Assert.Equal("RoadDamage", prediction.Category);
        Assert.Equal("High", prediction.Severity);
        Assert.Equal(0.91, prediction.Confidence);
        Assert.Equal("DOT", prediction.SuggestedAgencyCode);
        Assert.Equal("test-prompt-v1", prediction.PromptVersion);
        Assert.Contains(prediction.Evidence, evidence => evidence.Kind == "Text");
        Assert.NotNull(candidates);
        Assert.Equal(duplicate.Id, Assert.Single(candidates).CandidateIncidentId);
        Assert.Contains(prediction.Evidence, evidence => evidence.Kind == "Duplicate");
    }

    [Fact]
    public async Task Controlled_agent_workflow_uses_weather_and_persists_tool_evidence()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole blocking the right lane near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        var prediction = incident.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.91),
            new AgencyCode("DOT"),
            "High road damage report routed to DOT.",
            "test-analyzer",
            "1.0",
            "test-prompt-v1",
            42,
            DateTimeOffset.Parse("2026-07-23T11:55:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, weather: new FakeWeatherService());
        var workflow = provider.GetRequiredService<IControlledTriageAgentService>();

        var result = await workflow.RunAsync(incident.Id, CancellationToken.None);

        Assert.False(result.RequiresHumanReview);
        Assert.Equal("draft_work_order_ready", result.Status);
        Assert.NotNull(result.Weather);
        Assert.True(result.Weather.IsAvailable);
        Assert.NotNull(result.DraftWorkOrder);
        Assert.Equal("DOT", result.DraftWorkOrder.AgencyCode);
        Assert.Contains(result.ToolRuns, toolRun => toolRun.ToolName == "get_weather");
        Assert.Contains(prediction.EvidenceItems, evidence =>
            evidence.Kind == "AgentTool" && evidence.Title == "create_draft_work_order");
    }

    [Fact]
    public async Task Controlled_agent_workflow_does_not_block_draft_when_weather_is_unavailable()
    {
        var repository = new FakeIncidentRepository();
        var incident = Incident.Create(
            "Large pothole blocking the right lane near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-23T11:50:00Z"));
        incident.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.91),
            new AgencyCode("DOT"),
            "High road damage report routed to DOT.",
            "test-analyzer",
            "1.0",
            "test-prompt-v1",
            42,
            DateTimeOffset.Parse("2026-07-23T11:55:00Z"));
        await repository.AddAsync(incident, CancellationToken.None);

        using var provider = BuildProvider(repository, weather: new UnavailableWeatherService());
        var workflow = provider.GetRequiredService<IControlledTriageAgentService>();

        var result = await workflow.RunAsync(incident.Id, CancellationToken.None);

        Assert.False(result.RequiresHumanReview);
        Assert.Equal("draft_work_order_ready", result.Status);
        Assert.False(result.Weather?.IsAvailable);
        Assert.NotNull(result.DraftWorkOrder);
        Assert.Contains(result.ToolRuns, toolRun =>
            toolRun.ToolName == "get_weather" && toolRun.Status == "Unavailable");
    }

    [Fact]
    public async Task Forecast_incident_volume_uses_history_and_segment_filters()
    {
        var repository = new FakeIncidentRepository();
        await repository.AddAsync(
            CreateForecastIncident("Large pothole on Pine Street", "RoadDamage", "DOT", "2026-07-22T09:00:00Z"),
            CancellationToken.None);
        await repository.AddAsync(
            CreateForecastIncident("Road crack near market", "RoadDamage", "DOT", "2026-07-23T09:00:00Z"),
            CancellationToken.None);
        await repository.AddAsync(
            CreateForecastIncident("Trash pile near alley", "Sanitation", "SANITATION", "2026-07-23T10:00:00Z"),
            CancellationToken.None);

        using var provider = BuildProvider(repository);
        var forecasting = provider.GetRequiredService<IIncidentForecastingService>();

        var result = await forecasting.ForecastIncidentVolumeAsync(
            new IncidentForecastInput(7, 3, "RoadDamage", "DOT"),
            CancellationToken.None);

        Assert.Equal(7, result.History.Count);
        Assert.Equal(3, result.Forecast.Count);
        Assert.Equal(2, result.History.Sum(point => point.ActualCount ?? 0));
        Assert.Equal("category=RoadDamage, agency=DOT", result.Segment);
        Assert.All(result.Forecast, point => Assert.True(point.ForecastCount >= 0));
    }

    private static ServiceProvider BuildProvider(
        FakeIncidentRepository repository,
        IDuplicateIncidentSearchService? duplicateSearch = null,
        IIncidentRealtimeNotifier? realtime = null,
        IIncidentProcessingQueue? processingQueue = null,
        IWeatherService? weather = null)
    {
        var services = new ServiceCollection()
            .AddApplication()
            .AddSingleton<IIncidentRepository>(repository)
            .AddSingleton<IUnitOfWork, FakeUnitOfWork>()
            .AddSingleton<IClock>(new FixedClock(DateTimeOffset.Parse("2026-07-23T12:00:00Z")))
            .AddSingleton<IAiIncidentAnalyzer, FakeAiIncidentAnalyzer>()
            .AddSingleton<IIncidentMediaAnalyzer, FakeIncidentMediaAnalyzer>()
            .AddSingleton<ITextEmbeddingGenerator, FakeTextEmbeddingGenerator>()
            .AddSingleton(duplicateSearch ?? new FakeDuplicateIncidentSearchService())
            .AddSingleton(weather ?? new FakeWeatherService())
            .AddSingleton<IFileStorageService, FakeFileStorageService>();

        if (processingQueue is not null)
        {
            services.AddSingleton(processingQueue);
        }

        if (realtime is not null)
        {
            services.AddSingleton(realtime);
        }

        return services.BuildServiceProvider();
    }

    private static Incident CreateForecastIncident(
        string description,
        string category,
        string agencyCode,
        string createdAt)
    {
        var occurredAt = DateTimeOffset.Parse(createdAt);
        var incident = Incident.Create(
            description,
            new GeoPoint(40.7128, -74.0060),
            occurredAt);
        incident.AddTriagePrediction(
            new IncidentCategory(category),
            IncidentSeverity.Medium,
            new ConfidenceScore(0.88),
            new AgencyCode(agencyCode),
            $"{category} report routed to {agencyCode}.",
            "test-forecast-model",
            "1.0",
            "test-prompt",
            20,
            occurredAt.AddMinutes(1));

        return incident;
    }

    private sealed class FakeIncidentRepository : IIncidentRepository
    {
        private readonly Dictionary<Guid, Incident> _incidents = [];
        private readonly Dictionary<Guid, float[]> _textEmbeddings = [];

        public Task AddAsync(Incident incident, CancellationToken cancellationToken)
        {
            _incidents[incident.Id] = incident;
            return Task.CompletedTask;
        }

        public Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            _incidents.TryGetValue(incidentId, out var incident);
            return Task.FromResult(incident);
        }

        public Task<Incident?> GetByPublicTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken)
        {
            var normalizedTrackingCode = Incident.NormalizePublicTrackingCode(trackingCode);
            var incident = _incidents.Values.SingleOrDefault(candidate =>
                string.Equals(candidate.PublicTrackingCode, normalizedTrackingCode, StringComparison.Ordinal));

            return Task.FromResult(incident);
        }

        public Task<bool> PublicTrackingCodeExistsAsync(string trackingCode, CancellationToken cancellationToken)
        {
            var normalizedTrackingCode = Incident.NormalizePublicTrackingCode(trackingCode);
            var exists = _incidents.Values.Any(candidate =>
                string.Equals(candidate.PublicTrackingCode, normalizedTrackingCode, StringComparison.Ordinal));

            return Task.FromResult(exists);
        }

        public Task<IReadOnlyCollection<Incident>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Incident>>(_incidents.Values.ToArray());
        }

        public void Update(Incident entity)
        {
            _incidents[entity.Id] = entity;
        }

        public void Remove(Incident entity)
        {
            _incidents.Remove(entity.Id);
        }

        public Task<IReadOnlyCollection<Incident>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken)
        {
            var query = _incidents.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(criteria.Status))
            {
                query = query.Where(incident => string.Equals(incident.Status.ToString(), criteria.Status, StringComparison.OrdinalIgnoreCase));
            }

            var results = query
                .OrderByDescending(incident => incident.CreatedAt)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<Incident>>(results);
        }

        public void SetTextEmbedding(Incident incident, float[] embedding)
        {
            _textEmbeddings[incident.Id] = embedding;
        }

        public int? GetTextEmbeddingLength(Guid incidentId)
        {
            return _textEmbeddings.TryGetValue(incidentId, out var embedding)
                ? embedding.Length
                : null;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeAiIncidentAnalyzer : IAiIncidentAnalyzer
    {
        public Task<IncidentAnalysisResult> AnalyzeAsync(
            IncidentAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IncidentAnalysisResult(
                "RoadDamage",
                "High",
                0.91,
                "High RoadDamage report routed to DOT.",
                "DOT",
                "test-analyzer",
                "1.0",
                "test-prompt-v1",
                42,
                [
                    new IncidentAnalysisEvidence(
                        "Text",
                        "Category keyword match",
                        "Matched category term(s): pothole.",
                        0.91)
                ]));
        }
    }

    private sealed class FakeTextEmbeddingGenerator : ITextEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var embedding = new float[1024];
            embedding[0] = 1;

            return Task.FromResult(embedding);
        }
    }

    private sealed class FakeIncidentMediaAnalyzer : IIncidentMediaAnalyzer
    {
        public Task<IncidentMediaAnalysisResult> AnalyzeAsync(
            IncidentMediaAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IncidentMediaAnalysisResult(
                "Image evidence suggests road damage.",
                null,
                ["road damage", "pothole"],
                0.77,
                "test-media-analyzer",
                "1.0",
                33));
        }
    }

    private sealed class FakeDuplicateIncidentSearchService(Guid? duplicateIncidentId = null)
        : IDuplicateIncidentSearchService
    {
        public Task<IReadOnlyCollection<DuplicateIncidentCandidateResult>> FindDuplicatesAsync(
            IncidentAnalysisRequest request,
            IncidentAnalysisResult analysis,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<DuplicateIncidentCandidateResult> results = duplicateIncidentId is null
                ? []
                :
                [
                    new DuplicateIncidentCandidateResult(
                        duplicateIncidentId.Value,
                        0.88,
                        "Similar report text near the same coordinates.")
                ];

            return Task.FromResult(results);
        }
    }

    private sealed class FakeWeatherService : IWeatherService
    {
        public Task<WeatherObservationResult> GetCurrentConditionsAsync(
            double latitude,
            double longitude,
            DateTimeOffset observedNear,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WeatherObservationResult(
                true,
                "test-weather",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                "KNYC",
                "Light rain",
                23.5,
                12.2,
                "NE",
                1.8,
                "No active weather alerts.",
                null));
        }
    }

    private sealed class UnavailableWeatherService : IWeatherService
    {
        public Task<WeatherObservationResult> GetCurrentConditionsAsync(
            double latitude,
            double longitude,
            DateTimeOffset observedNear,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(WeatherObservationResult.Unavailable(
                "test-weather",
                "Weather provider disabled.",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")));
        }
    }

    private sealed class RecordingIncidentRealtimeNotifier : IIncidentRealtimeNotifier
    {
        public List<IncidentRealtimeEventDto> Events { get; } = [];

        public Task PublishAsync(
            IncidentRealtimeEventDto incidentEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(incidentEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingIncidentProcessingQueue : IIncidentProcessingQueue
    {
        public List<QueuedIncidentProcessingMessage> Messages { get; } = [];

        public Task EnqueueAsync(
            Guid incidentId,
            string trigger,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new QueuedIncidentProcessingMessage(incidentId, trigger));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedIncidentProcessingMessage(Guid IncidentId, string Trigger);

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task<StoredFileInfo> StoreAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StoredFileInfo(fileName, contentType, $"placeholder://{fileName}"));
        }

        public Task<Stream?> OpenReadAsync(
            string storageUri,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream?>(new MemoryStream("fake image"u8.ToArray()));
        }
    }
}
