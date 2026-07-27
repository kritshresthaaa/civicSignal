using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class IncidentMediaUploadTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid IncidentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string TrackingCode = "CS-ABCD-2345";

    [Fact]
    public async Task Upload_media_accepts_multipart_file_and_attaches_media()
    {
        var intelligence = new FakeIncidentIntelligenceService();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentIntelligenceService>();
                services.RemoveAll<IFileStorageService>();
                services.RemoveAll<IIncidentService>();
                services.AddSingleton<IIncidentIntelligenceService>(intelligence);
                services.AddSingleton<IFileStorageService, FakeFileStorageService>();
                services.AddSingleton<IIncidentService, FakeIncidentService>();
            });
        });
        var client = app.CreateClient();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent("fake-image-bytes"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "street.png");

        var response = await client.PostAsync($"/api/public/incidents/{TrackingCode}/media/upload", content);

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var media = await response.Content.ReadFromJsonAsync<IncidentMediaDto>();
        Assert.NotNull(media);
        Assert.Equal(IncidentId, media.IncidentId);
        Assert.Equal("street.png", intelligence.AddedMediaInput?.FileName);
        Assert.Equal("/media/street.png", intelligence.AddedMediaInput?.StorageUri);
    }

    private sealed class FakeIncidentService : IIncidentService
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

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task<StoredFileInfo> StoreAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StoredFileInfo(fileName, contentType, $"/media/{fileName}"));
        }

        public Task<Stream?> OpenReadAsync(
            string storageUri,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream?>(new MemoryStream("fake-image-bytes"u8.ToArray()));
        }
    }

    private sealed class FakeIncidentIntelligenceService : IIncidentIntelligenceService
    {
        public AddIncidentMediaInput? AddedMediaInput { get; private set; }

        public Task<IncidentMediaDto> AddMediaAsync(
            Guid incidentId,
            AddIncidentMediaInput input,
            CancellationToken cancellationToken = default)
        {
            AddedMediaInput = input;

            return Task.FromResult(new IncidentMediaDto(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                incidentId,
                input.FileName,
                input.ContentType,
                input.StorageUri,
                "Image",
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
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")));
        }

        public Task<IReadOnlyCollection<IncidentMediaDto>?> GetMediaAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<DuplicateCandidateDto>?> GetDuplicateCandidatesAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
