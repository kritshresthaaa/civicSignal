using CivicSignal.Application.Incidents;
using CivicSignal.Infrastructure.Messaging;
using CivicSignal.Worker.Options;
using CivicSignal.Worker.Processing;
using Microsoft.Extensions.Options;

namespace CivicSignal.Worker;

public sealed class IncidentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IncidentProcessingWorkerOptions> options,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<IncidentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (rabbitMqOptions.Value.Enabled)
        {
            logger.LogInformation("Database polling worker is disabled because RabbitMQ incident processing is enabled.");
            return;
        }

        if (!options.Value.Enabled)
        {
            logger.LogInformation("Incident processing worker is disabled.");
            return;
        }

        logger.LogInformation("Incident processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Incident processing worker batch failed.");
            }

            await Task.Delay(options.Value.PollingInterval, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IncidentProcessingPipeline>();

        var candidates = await incidents.SearchAsync(
            new IncidentSearchInput(Status: "Submitted", Page: 1, PageSize: options.Value.NormalizedBatchSize),
            cancellationToken);

        foreach (var incident in candidates)
        {
            await pipeline.ProcessAsync(incident, "Polling", cancellationToken);
        }

        if (candidates.Count > 0)
        {
            logger.LogInformation("Processed {IncidentCount} submitted incidents.", candidates.Count);
        }

        return candidates.Count;
    }
}
