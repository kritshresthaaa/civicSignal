using CivicSignal.Application.DataImports;
using CivicSignal.Infrastructure.Messaging;
using CivicSignal.Worker.Options;
using Microsoft.Extensions.Options;

namespace CivicSignal.Worker.DataImports;

public sealed class DataImportJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DataImportWorkerOptions> options,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<DataImportJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (rabbitMqOptions.Value.Enabled)
        {
            logger.LogInformation("Data import polling worker is disabled because RabbitMQ is enabled.");
            return;
        }

        if (!options.Value.Enabled)
        {
            logger.LogInformation("Data import worker is disabled.");
            return;
        }

        logger.LogInformation("Data import polling worker started.");

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
                logger.LogError(exception, "Data import worker batch failed.");
            }

            await Task.Delay(options.Value.PollingInterval, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IDataImportJobService>();
        var processedCount = await jobs.RunPendingAsync(options.Value.NormalizedBatchSize, cancellationToken);

        if (processedCount > 0)
        {
            logger.LogInformation("Processed {JobCount} data import job(s).", processedCount);
        }

        return processedCount;
    }
}
