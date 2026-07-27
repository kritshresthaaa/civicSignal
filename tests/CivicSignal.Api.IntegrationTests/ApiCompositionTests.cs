using System.Net.Http.Json;
using CivicSignal.Api.Contracts.System;
using CivicSignal.Application.Abstractions.Geocoding;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using CivicSignal.Application.ModelLab.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class ApiCompositionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Swagger_ui_is_available_in_development()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Swagger_json_is_available_in_development()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("CivicSignal API", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/register", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/login", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/refresh", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/logout", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/me", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/csrf", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/status", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/media", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/media/upload", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/analyze", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/agent-workflow", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/prediction", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/duplicates", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/similar", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/processing-status", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/update-requests", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/notification-preference", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/feedback", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/review", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/incidents/{incidentId}/reviews", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/status", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/media/upload", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/prediction", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/duplicates", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/update-requests", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/notification-preference", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/public/incidents/{trackingCode}/feedback", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/historical-complaints", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/historical-complaints/summary", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/historical-complaints/nyc311/import", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/data-import-jobs", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/data-import-jobs/nyc311", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/data-import-jobs/{jobId}/retry", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/geocoding/search", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/geocoding/reverse", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/model-lab/analyze", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/ai-evaluations/baselines", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/system/capabilities", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/system/integrations", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/system/runtime-policy", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Capabilities_endpoint_lists_data_source_routes()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/system/capabilities");

        response.EnsureSuccessStatusCode();
        var capabilities = await response.Content.ReadFromJsonAsync<SystemCapabilitiesResponse>();

        Assert.NotNull(capabilities);
        Assert.Contains("public-incident-feed", capabilities.Features);
        Assert.Contains("historical-complaints", capabilities.Features);
        Assert.Contains("data-import-jobs", capabilities.Features);
        Assert.Contains("weather-context", capabilities.Features);
        Assert.Contains("osm-nominatim-geocoding", capabilities.Features);
        Assert.Contains("controlled-agent-workflow", capabilities.Features);
        Assert.Contains("model-lab-classifier", capabilities.Features);
        Assert.Contains("ai-evaluation-baselines", capabilities.Features);
        Assert.Contains("ai-evaluation-quality-gates", capabilities.Features);
        Assert.Contains("api/historical-complaints/summary", capabilities.Routes);
        Assert.Contains("api/data-import-jobs", capabilities.Routes);
        Assert.Contains("api/data-import-jobs/nyc311", capabilities.Routes);
        Assert.Contains("api/geocoding/search", capabilities.Routes);
        Assert.Contains("api/geocoding/reverse", capabilities.Routes);
        Assert.Contains("api/model-lab/analyze", capabilities.Routes);
        Assert.Contains("api/incidents/{incidentId:guid}/agent-workflow", capabilities.Routes);
        Assert.Contains("api/ai-evaluations/baselines", capabilities.Routes);
        Assert.Contains("api/system/integrations", capabilities.Routes);
        Assert.Contains("api/system/runtime-policy", capabilities.Routes);
    }

    [Fact]
    public async Task System_integration_and_runtime_policy_endpoints_are_public()
    {
        var client = factory.CreateClient();

        var integrationsResponse = await client.GetAsync("/api/system/integrations");
        var policyResponse = await client.GetAsync("/api/system/runtime-policy");

        integrationsResponse.EnsureSuccessStatusCode();
        policyResponse.EnsureSuccessStatusCode();
        var integrationsJson = await integrationsResponse.Content.ReadAsStringAsync();
        var policyJson = await policyResponse.Content.ReadAsStringAsync();

        Assert.Contains("PostgreSQL/PostGIS", integrationsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Python AI service", integrationsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicateMinimumScore", policyJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("textEmbeddingDimensions", policyJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_endpoint_is_available()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Cors_allows_local_next_development_origin()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins),
            "Expected the API to return a CORS allow-origin header.");
        Assert.Contains("http://localhost:3000", origins);
    }

    [Fact]
    public async Task Incident_search_requires_authentication_with_local_next_development_origin()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.AddSingleton<IIncidentService, EmptyIncidentService>();
            });
        });
        var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/incidents?pageSize=100");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins),
            "Expected the incident search endpoint to return a CORS allow-origin header.");
        Assert.Contains("http://localhost:3000", origins);
    }

    [Fact]
    public async Task Citizen_engagement_endpoints_are_public()
    {
        var incidentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        const string trackingCode = "CS-ABCD-2345";
        var incidentService = new CitizenEngagementIncidentService();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.AddSingleton<IIncidentService>(incidentService);
            });
        });
        var client = app.CreateClient();

        var updateResponse = await client.PostAsJsonAsync(
            $"/api/public/incidents/{trackingCode}/update-requests",
            new { message = "Could you notify me when a crew is assigned?" });
        var preferenceResponse = await client.PutAsJsonAsync(
            $"/api/public/incidents/{trackingCode}/notification-preference",
            new { alertsEnabled = true, channel = "Browser" });
        var feedbackResponse = await client.PostAsJsonAsync(
            $"/api/public/incidents/{trackingCode}/feedback",
            new { rating = 5, comment = "The status page was clear." });
        var feedbackListResponse = await client.GetAsync($"/api/public/incidents/{trackingCode}/feedback");

        updateResponse.EnsureSuccessStatusCode();
        preferenceResponse.EnsureSuccessStatusCode();
        feedbackResponse.EnsureSuccessStatusCode();
        feedbackListResponse.EnsureSuccessStatusCode();
        var updateRequest = await updateResponse.Content.ReadFromJsonAsync<IncidentUpdateRequestDto>();
        var preference = await preferenceResponse.Content.ReadFromJsonAsync<IncidentNotificationPreferenceDto>();
        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<IncidentFeedbackDto>();
        var feedbackList = await feedbackListResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<IncidentFeedbackDto>>();

        Assert.NotNull(updateRequest);
        Assert.NotNull(preference);
        Assert.NotNull(feedback);
        Assert.NotNull(feedbackList);
        Assert.Equal(incidentId, updateRequest.IncidentId);
        Assert.Equal("Could you notify me when a crew is assigned?", incidentService.UpdateMessage);
        Assert.True(preference.AlertsEnabled);
        Assert.Equal("Browser", incidentService.NotificationChannel);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal("The status page was clear.", incidentService.FeedbackComment);
        Assert.Contains(feedbackList, item => item.Comment == "I also see this issue near the crosswalk.");
    }

    [Fact]
    public async Task Public_incident_feed_is_anonymous_and_redacts_staff_fields()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.RemoveAll<IIncidentIntelligenceService>();
                services.AddSingleton<IIncidentService, PublicFeedIncidentService>();
                services.AddSingleton<IIncidentIntelligenceService, PublicFeedIncidentIntelligenceService>();
            });
        });
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/public/incidents?pageSize=5");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("CS-FEED-0001", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"approximateLatitude\":40.713", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"approximateLongitude\":-74.006", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"category\":\"RoadDamage\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"agencyCode\":\"DOT\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"supportCount\":1", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"commentCount\":2", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewNote", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewedByUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignedByUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duplicateOfIncidentId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("40.712846", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Geocoding_endpoints_are_public_and_use_application_abstraction()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGeocodingService>();
                services.AddSingleton<IGeocodingService, FakeGeocodingService>();
            });
        });
        var client = app.CreateClient();

        var searchResponse = await client.GetAsync("/api/geocoding/search?query=City%20Hall");
        var reverseResponse = await client.GetAsync("/api/geocoding/reverse?latitude=40.7128&longitude=-74.0060");

        searchResponse.EnsureSuccessStatusCode();
        reverseResponse.EnsureSuccessStatusCode();
        var searchResults = await searchResponse.Content.ReadFromJsonAsync<GeocodingResult[]>();
        var reverseResult = await reverseResponse.Content.ReadFromJsonAsync<GeocodingResult>();

        Assert.NotNull(searchResults);
        Assert.Single(searchResults);
        Assert.NotNull(reverseResult);
        Assert.Equal("City Hall Park, New York, NY", searchResults.Single().DisplayName);
        Assert.Equal("Device location, New York, NY", reverseResult.DisplayName);
    }

    [Fact]
    public async Task Model_lab_analyze_endpoint_is_public()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/model-lab/analyze",
            new { text = "Large pothole on Pine Street forcing cars to swerve.", embeddingDimensions = 16 });

        response.EnsureSuccessStatusCode();
        var analysis = await response.Content.ReadFromJsonAsync<ModelLabAnalysisDto>();

        Assert.NotNull(analysis);
        Assert.Equal("RoadDamage", analysis.PredictedCategory);
        Assert.Equal("DOT", analysis.SuggestedAgencyCode);
        Assert.Contains(analysis.Tokens, token => token.Normalized == "pothole");
    }

    private sealed class EmptyIncidentService : IIncidentService
    {
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
            return Task.FromResult<IReadOnlyCollection<IncidentDto>>([]);
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
            throw new NotSupportedException();
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

    private sealed class PublicFeedIncidentService : IIncidentService
    {
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
            IReadOnlyCollection<IncidentDto> results =
            [
                new IncidentDto(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    "CS-FEED-0001",
                    "Large pothole blocking the curb lane near City Hall.",
                    40.712846,
                    -74.006023,
                    "Triaged",
                    DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                    "Approved",
                    "Internal staff note should never appear in the public feed.",
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    DateTimeOffset.Parse("2026-07-23T12:10:00Z"),
                    null,
                    null,
                    null,
                    null,
                    true,
                    AssignedByUserId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"))
            ];

            return Task.FromResult(results);
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
            throw new NotSupportedException();
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
            IReadOnlyCollection<IncidentFeedbackDto> feedback =
            [
                new IncidentFeedbackDto(
                    Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    incidentId,
                    5,
                    null,
                    DateTimeOffset.Parse("2026-07-23T12:02:00Z")),
                new IncidentFeedbackDto(
                    Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                    incidentId,
                    4,
                    "Standing water is spreading toward the crosswalk.",
                    DateTimeOffset.Parse("2026-07-23T12:03:00Z")),
                new IncidentFeedbackDto(
                    Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    incidentId,
                    3,
                    "Please add cones before rush hour.",
                    DateTimeOffset.Parse("2026-07-23T12:04:00Z"))
            ];

            return Task.FromResult<IReadOnlyCollection<IncidentFeedbackDto>?>(feedback);
        }

        public Task<IncidentFeedbackDto> AddFeedbackAsync(
            Guid incidentId,
            CreateIncidentFeedbackInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class PublicFeedIncidentIntelligenceService : IIncidentIntelligenceService
    {
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
            return Task.FromResult<IReadOnlyCollection<IncidentMediaDto>?>([]);
        }

        public Task<IncidentMediaDto> AnalyzeMediaAsync(
            Guid incidentId,
            Guid mediaId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TriagePredictionDto> AnalyzeAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TriagePredictionDto?> GetLatestPredictionAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TriagePredictionDto?>(new TriagePredictionDto(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                incidentId,
                "RoadDamage",
                "Medium",
                0.82,
                "Road damage likely affects the curb lane.",
                "DOT",
                "FakeModel",
                "test",
                "test",
                12,
                DateTimeOffset.Parse("2026-07-23T12:01:00Z"),
                []));
        }

        public Task<IReadOnlyCollection<DuplicateCandidateDto>?> GetDuplicateCandidatesAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeGeocodingService : IGeocodingService
    {
        public Task<IReadOnlyCollection<GeocodingResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<GeocodingResult> results =
            [
                new GeocodingResult(
                    "City Hall Park, New York, NY",
                    40.7128,
                    -74.0060,
                    "place",
                    "park",
                    0.83,
                    "City Hall Park",
                    "New York",
                    "NY",
                    "10007",
                    "United States")
            ];

            return Task.FromResult(results);
        }

        public Task<GeocodingResult?> ReverseAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GeocodingResult?>(new GeocodingResult(
                "Device location, New York, NY",
                latitude,
                longitude,
                "highway",
                "residential",
                0.7,
                "Pine Street",
                "New York",
                "NY",
                "10005",
                "United States"));
        }
    }

    private sealed class CitizenEngagementIncidentService : IIncidentService
    {
        public string? UpdateMessage { get; private set; }

        public string? NotificationChannel { get; private set; }

        public string? FeedbackComment { get; private set; }

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
            return Task.FromResult<IncidentDto?>(new IncidentDto(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                trackingCode,
                "Large pothole near Main Street",
                40.7128,
                -74.0060,
                "Submitted",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
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
            throw new NotSupportedException();
        }

        public Task<IncidentUpdateRequestDto> RequestUpdateAsync(
            Guid incidentId,
            CreateIncidentUpdateRequestInput input,
            CancellationToken cancellationToken = default)
        {
            UpdateMessage = input.Message;

            return Task.FromResult(new IncidentUpdateRequestDto(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                incidentId,
                input.Message,
                "Open",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")));
        }

        public Task<IncidentNotificationPreferenceDto> UpdateNotificationPreferenceAsync(
            Guid incidentId,
            UpdateNotificationPreferenceInput input,
            CancellationToken cancellationToken = default)
        {
            NotificationChannel = input.Channel;

            return Task.FromResult(new IncidentNotificationPreferenceDto(
                incidentId,
                input.AlertsEnabled,
                input.Channel ?? "None",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")));
        }

        public Task<IReadOnlyCollection<IncidentFeedbackDto>?> GetFeedbackAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<IncidentFeedbackDto> feedback =
            [
                new IncidentFeedbackDto(
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    incidentId,
                    5,
                    "I also see this issue near the crosswalk.",
                    DateTimeOffset.Parse("2026-07-23T12:05:00Z"))
            ];

            return Task.FromResult<IReadOnlyCollection<IncidentFeedbackDto>?>(feedback);
        }

        public Task<IncidentFeedbackDto> AddFeedbackAsync(
            Guid incidentId,
            CreateIncidentFeedbackInput input,
            CancellationToken cancellationToken = default)
        {
            FeedbackComment = input.Comment;

            return Task.FromResult(new IncidentFeedbackDto(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                incidentId,
                input.Rating,
                input.Comment,
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")));
        }
    }
}
