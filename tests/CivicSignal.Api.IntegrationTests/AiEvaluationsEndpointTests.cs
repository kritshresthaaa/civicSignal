using System.Net;
using CivicSignal.Application.AiEvaluations;
using CivicSignal.Application.AiEvaluations.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class AiEvaluationsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Baseline_report_requires_staff_authorization()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiEvaluationService>();
                services.AddSingleton<IAiEvaluationService, FakeAiEvaluationService>();
            });
        });
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/ai-evaluations/baselines");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class FakeAiEvaluationService : IAiEvaluationService
    {
        public Task<AiEvaluationBaselineReportDto> GetBaselineReportAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
