using System.Text.Json;
using CivicSignal.Application.DataImports;
using CivicSignal.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CivicSignal.Worker.Messaging;

public sealed class RabbitMqDataImportJobConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqDataImportJobConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("RabbitMQ data import consumer is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ data import consumer stopped unexpectedly. Restarting shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var rabbitMqOptions = options.Value;
        var connectionFactory = RabbitMqTopology.CreateConnectionFactory(rabbitMqOptions);

        await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitMqTopology.DeclareAsync(channel, rabbitMqOptions, stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: rabbitMqOptions.NormalizedPrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
            await HandleDeliveryAsync(channel, delivery, rabbitMqOptions, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: rabbitMqOptions.NormalizedDataImportQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "RabbitMQ data import consumer is listening on queue {QueueName}.",
            rabbitMqOptions.NormalizedDataImportQueueName);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        RabbitMqOptions rabbitMqOptions,
        CancellationToken cancellationToken)
    {
        DataImportJobQueueMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<DataImportJobQueueMessage>(
                delivery.Body.Span,
                JsonOptions);

            if (message is null || message.JobId == Guid.Empty)
            {
                logger.LogWarning("Rejecting invalid RabbitMQ data import message.");
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            await ProcessMessageAsync(message, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogInformation(
                "Processed RabbitMQ data import message {MessageId} for job {JobId}.",
                message.MessageId,
                message.JobId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(channel, delivery, rabbitMqOptions, message, exception, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(
        DataImportJobQueueMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IDataImportJobService>();

        await jobs.RunJobAsync(message.JobId, cancellationToken);
    }

    private async Task HandleFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        RabbitMqOptions rabbitMqOptions,
        DataImportJobQueueMessage? message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (message is null)
        {
            logger.LogError(exception, "Could not deserialize RabbitMQ data import message.");
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        var error = exception.Message.Length > 1_000
            ? exception.Message[..1_000]
            : exception.Message;
        var nextMessage = message.ForRetry(error);

        try
        {
            if (nextMessage.Attempt <= rabbitMqOptions.NormalizedMaxRetryAttempts)
            {
                await PublishAsync(
                    channel,
                    rabbitMqOptions.NormalizedDataImportRetryExchangeName,
                    rabbitMqOptions.NormalizedDataImportRetryRoutingKey,
                    nextMessage,
                    cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

                logger.LogWarning(
                    exception,
                    "Data import message {MessageId} for job {JobId} failed. Retry attempt {Attempt}/{MaxAttempts} queued.",
                    message.MessageId,
                    message.JobId,
                    nextMessage.Attempt,
                    rabbitMqOptions.NormalizedMaxRetryAttempts);
                return;
            }

            await PublishAsync(
                channel,
                rabbitMqOptions.NormalizedDataImportDeadLetterExchangeName,
                rabbitMqOptions.NormalizedDataImportDeadLetterRoutingKey,
                nextMessage,
                cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogError(
                exception,
                "Data import message {MessageId} for job {JobId} exhausted retries and was dead-lettered.",
                message.MessageId,
                message.JobId);
        }
        catch (Exception publishException)
        {
            logger.LogError(
                publishException,
                "Could not publish retry or dead-letter message for data import job {JobId}; requeueing original delivery.",
                message.JobId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static async Task PublishAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        DataImportJobQueueMessage message,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("N"),
            Persistent = true,
            Type = nameof(DataImportJobQueueMessage)
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
