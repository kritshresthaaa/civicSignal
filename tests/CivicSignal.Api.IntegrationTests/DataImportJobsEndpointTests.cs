using System.Net;
using System.Net.Http.Json;
using CivicSignal.Application.DataImports;
using CivicSignal.Application.DataImports.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class DataImportJobsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Queue_data_import_job_requires_operations_authorization()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDataImportJobService>();
                services.AddSingleton<IDataImportJobService, FakeDataImportJobService>();
            });
        });
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/data-import-jobs/nyc311",
            new { limit = 10, daysBack = 7 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Retry_data_import_job_requires_operations_authorization()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDataImportJobService>();
                services.AddSingleton<IDataImportJobService, FakeDataImportJobService>();
            });
        });
        var client = app.CreateClient();
        var jobId = Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48");

        var response = await client.PostAsync(
            $"/api/data-import-jobs/{jobId}/retry",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class FakeDataImportJobService : IDataImportJobService
    {
        public Task<DataImportJobDto> QueueNyc311ImportAsync(
            CreateNyc311ImportJobInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DataImportJobDto?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<DataImportJobDto>> SearchAsync(
            DataImportJobSearchInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DataImportJobDto> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RunPendingAsync(int count, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DataImportJobDto> RunJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
