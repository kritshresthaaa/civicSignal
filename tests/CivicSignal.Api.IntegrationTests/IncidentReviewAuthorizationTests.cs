using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CivicSignal.Api.IntegrationTests;

public sealed class IncidentReviewAuthorizationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid IncidentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ReviewerId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");

    [Fact]
    public async Task Review_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/review",
            new { decision = "Approved", note = "Confirmed by reviewer." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Review_forbids_reporter_role()
    {
        using var app = CreateAuthorizedFactory("Reporter", new FakeIncidentService());
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/review",
            new { decision = "Approved", note = "Confirmed by reviewer." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Review_allows_reviewer_role()
    {
        var incidentService = new FakeIncidentService();
        using var app = CreateAuthorizedFactory("Reviewer", incidentService);
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/review",
            new
            {
                decision = "Approved",
                note = "Confirmed by reviewer.",
                correctedCategory = "RoadDamage",
                correctedAgencyCode = "DOT",
                correctedSeverity = "High",
                duplicateOfIncidentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                acceptedPrediction = false
            });

        response.EnsureSuccessStatusCode();
        var reviewed = await response.Content.ReadFromJsonAsync<IncidentDto>();

        Assert.NotNull(reviewed);
        Assert.Equal("Approved", reviewed.Status);
        Assert.Equal("RoadDamage", reviewed.CorrectedCategory);
        Assert.Equal("DOT", reviewed.CorrectedAgencyCode);
        Assert.Equal("High", reviewed.CorrectedSeverity);
        Assert.False(reviewed.AcceptedPrediction);
        Assert.Equal(IncidentId, incidentService.ReviewedIncidentId);
        Assert.Equal(ReviewerId, incidentService.ReviewerUserId);
    }

    [Fact]
    public async Task Processing_status_read_by_raw_incident_id_requires_authentication()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.AddSingleton<IIncidentService>(new FakeIncidentService());
            });
        });
        var client = app.CreateClient();

        var response = await client.GetAsync($"/api/incidents/{IncidentId}/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Processing_status_read_by_tracking_code_is_public()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.AddSingleton<IIncidentService>(new FakeIncidentService());
            });
        });
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/public/incidents/CS-ABCD-2345/status");

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<IncidentProcessingStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(IncidentId, status.IncidentId);
    }

    [Fact]
    public async Task Processing_status_update_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/processing-status",
            new { stepName = "MediaAnalysis", status = "InProgress", errorMessage = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Processing_status_update_forbids_reporter_role()
    {
        using var app = CreateAuthorizedFactory("Reporter", new FakeIncidentService());
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/processing-status",
            new { stepName = "MediaAnalysis", status = "InProgress", errorMessage = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Processing_status_update_allows_operator_role()
    {
        var incidentService = new FakeIncidentService();
        using var app = CreateAuthorizedFactory("Operator", incidentService);
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/processing-status",
            new { stepName = "MediaAnalysis", status = "InProgress", errorMessage = (string?)null });

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<IncidentProcessingStatusDto>();

        Assert.NotNull(status);
        Assert.Equal("Processing", status.IncidentStatus);
        Assert.Equal(IncidentId, incidentService.ProcessingIncidentId);
        Assert.Equal("MediaAnalysis", incidentService.ProcessingStepName);
    }

    [Fact]
    public async Task Assign_allows_operator_role()
    {
        var incidentService = new FakeIncidentService();
        using var app = CreateAuthorizedFactory("Operator", incidentService);
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{IncidentId}/assign",
            new
            {
                assignedTeam = "DOT intake queue",
                assignedAgencyCode = "DOT",
                note = "Assigned from operations console."
            });

        response.EnsureSuccessStatusCode();
        var incident = await response.Content.ReadFromJsonAsync<IncidentDto>();

        Assert.NotNull(incident);
        Assert.Equal("DOT intake queue", incident.AssignedTeam);
        Assert.Equal("DOT", incident.AssignedAgencyCode);
        Assert.Equal(IncidentId, incidentService.AssignedIncidentId);
        Assert.Equal(ReviewerId, incidentService.AssignedByUserId);
    }

    private WebApplicationFactory<Program> CreateAuthorizedFactory(string role, IIncidentService incidentService)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentService>();
                services.AddSingleton(incidentService);

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<TestAuthOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options =>
                    {
                        options.Role = role;
                        options.UserId = ReviewerId;
                    });
            });
        });
    }

    private sealed class FakeIncidentService : IIncidentService
    {
        public Guid? ReviewedIncidentId { get; private set; }

        public Guid? ReviewerUserId { get; private set; }

        public Guid? ProcessingIncidentId { get; private set; }

        public string? ProcessingStepName { get; private set; }

        public Guid? AssignedIncidentId { get; private set; }

        public Guid? AssignedByUserId { get; private set; }

        public Guid? DispatchedIncidentId { get; private set; }

        public Guid? DuplicateLinkedIncidentId { get; private set; }

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
                IncidentId,
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
            ReviewedIncidentId = incidentId;
            ReviewerUserId = input.ReviewerUserId;

            return Task.FromResult(new IncidentDto(
                incidentId,
                "CS-ABCD-2345",
                "Large pothole near Main Street",
                40.7128,
                -74.0060,
                "Approved",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                "Approved",
                input.Note,
                input.ReviewerUserId,
                DateTimeOffset.Parse("2026-07-23T12:10:00Z"),
                input.CorrectedCategory,
                input.CorrectedAgencyCode,
                input.CorrectedSeverity,
                input.DuplicateOfIncidentId,
                input.AcceptedPrediction));
        }

        public Task<IncidentDto> AssignAsync(
            Guid incidentId,
            AssignIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            AssignedIncidentId = incidentId;
            AssignedByUserId = input.AssignedByUserId;

            return Task.FromResult(new IncidentDto(
                incidentId,
                "CS-ABCD-2345",
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
                null,
                AssignedAgencyCode: input.AssignedAgencyCode,
                AssignedTeam: input.AssignedTeam,
                AssignedByUserId: input.AssignedByUserId,
                AssignedAt: DateTimeOffset.Parse("2026-07-23T12:11:00Z")));
        }

        public Task<IncidentDto> DispatchAsync(
            Guid incidentId,
            DispatchIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            DispatchedIncidentId = incidentId;

            return Task.FromResult(new IncidentDto(
                incidentId,
                "CS-ABCD-2345",
                "Large pothole near Main Street",
                40.7128,
                -74.0060,
                "Dispatched",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                DispatchedByUserId: input.DispatchedByUserId,
                DispatchedAt: DateTimeOffset.Parse("2026-07-23T12:12:00Z")));
        }

        public Task<IncidentDto> LinkDuplicateAsync(
            Guid incidentId,
            LinkDuplicateIncidentInput input,
            CancellationToken cancellationToken = default)
        {
            DuplicateLinkedIncidentId = incidentId;

            return Task.FromResult(new IncidentDto(
                incidentId,
                "CS-ABCD-2345",
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
                input.DuplicateOfIncidentId,
                null,
                DuplicateLinkedByUserId: input.LinkedByUserId,
                DuplicateLinkedAt: DateTimeOffset.Parse("2026-07-23T12:13:00Z")));
        }

        public Task<IReadOnlyCollection<IncidentReviewDto>?> GetReviewHistoryAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<IncidentReviewDto> reviews =
            [
                new IncidentReviewDto(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    incidentId,
                    "Approved",
                    "Confirmed by reviewer.",
                    ReviewerId,
                    "RoadDamage",
                    "DOT",
                    "High",
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    false,
                    DateTimeOffset.Parse("2026-07-23T12:10:00Z"))
            ];

            return Task.FromResult<IReadOnlyCollection<IncidentReviewDto>?>(reviews);
        }

        public Task<IncidentProcessingStatusDto?> GetProcessingStatusAsync(Guid incidentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IncidentProcessingStatusDto?>(new IncidentProcessingStatusDto(
                incidentId,
                "Submitted",
                []));
        }

        public Task<IncidentProcessingStatusDto> UpdateProcessingStatusAsync(
            Guid incidentId,
            UpdateProcessingStatusInput input,
            CancellationToken cancellationToken = default)
        {
            ProcessingIncidentId = incidentId;
            ProcessingStepName = input.StepName;

            return Task.FromResult(new IncidentProcessingStatusDto(
                incidentId,
                "Processing",
                [
                    new ProcessingStepDto(
                        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        input.StepName,
                        input.Status,
                        DateTimeOffset.Parse("2026-07-23T12:01:00Z"),
                        null,
                        input.ErrorMessage,
                        DateTimeOffset.Parse("2026-07-23T12:01:00Z"))
                ]));
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

    private sealed class TestAuthOptions : AuthenticationSchemeOptions
    {
        public string Role { get; set; } = "Reporter";

        public Guid UserId { get; set; } = ReviewerId;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<TestAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<TestAuthOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Options.UserId.ToString()),
                new Claim(ClaimTypes.Name, "reviewer@example.com"),
                new Claim(ClaimTypes.Email, "reviewer@example.com"),
                new Claim(ClaimTypes.Role, Options.Role)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
