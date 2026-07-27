using System.Net;
using System.Net.Http.Json;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Application.HistoricalComplaints.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class HistoricalComplaintsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Historical_complaint_search_is_public_and_uses_service_filters()
    {
        var service = new FakeHistoricalComplaintService();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHistoricalComplaintService>();
                services.AddSingleton<IHistoricalComplaintService>(service);
            });
        });
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/historical-complaints?category=RoadDamage&pageSize=5");

        response.EnsureSuccessStatusCode();
        var complaints = await response.Content.ReadFromJsonAsync<HistoricalComplaintDto[]>();

        Assert.NotNull(complaints);
        Assert.Equal("RoadDamage", service.SearchInput?.Category);
        Assert.Equal(5, service.SearchInput?.PageSize);
        Assert.Equal("311-1", Assert.Single(complaints).ExternalId);
    }

    [Fact]
    public async Task Historical_complaint_import_requires_operations_authorization()
    {
        var service = new FakeHistoricalComplaintService();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHistoricalComplaintService>();
                services.AddSingleton<IHistoricalComplaintService>(service);
            });
        });
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/historical-complaints/nyc311/import",
            new { limit = 10, daysBack = 7 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class FakeHistoricalComplaintService : IHistoricalComplaintService
    {
        public HistoricalComplaintSearchInput? SearchInput { get; private set; }

        public Task<IReadOnlyCollection<HistoricalComplaintDto>> SearchAsync(
            HistoricalComplaintSearchInput input,
            CancellationToken cancellationToken = default)
        {
            SearchInput = input;

            IReadOnlyCollection<HistoricalComplaintDto> results =
            [
                new HistoricalComplaintDto(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    "NYC311",
                    "311-1",
                    "RoadDamage",
                    "Street Condition",
                    "Pothole",
                    "DOT",
                    "Department of Transportation",
                    "Open",
                    "MANHATTAN",
                    "Main Street",
                    null,
                    40.7128,
                    -74.0060,
                    DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                    null,
                    DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                    DateTimeOffset.Parse("2026-07-23T12:00:00Z"))
            ];

            return Task.FromResult(results);
        }

        public Task<HistoricalComplaintSummaryDto> GetSummaryAsync(
            HistoricalComplaintSearchInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HistoricalComplaintSummaryDto(
                1,
                DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                [new HistoricalComplaintBucketDto("RoadDamage", 1)],
                [new HistoricalComplaintBucketDto("DOT", 1)],
                [new HistoricalComplaintBucketDto("MANHATTAN", 1)]));
        }

        public Task<HistoricalComplaintImportResultDto> ImportNyc311Async(
            ImportNyc311ComplaintsInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HistoricalComplaintImportResultDto(
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                1,
                1,
                0,
                0));
        }
    }
}
