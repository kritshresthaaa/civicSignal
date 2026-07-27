using System.Text.Json;
using CivicSignal.Application.Incidents;
using CivicSignal.Infrastructure.Messaging;
using CivicSignal.Worker.Processing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CivicSignal.Worker.Messaging;

public sealed class RabbitMqIncidentProcessingConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqIncidentProcessingConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("RabbitMQ incident consumer is disabled.");
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
                logger.LogError(exception, "RabbitMQ incident consumer stopped unexpectedly. Restarting shortly.");
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
            queue: rabbitMqOptions.NormalizedQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "RabbitMQ incident consumer is listening on queue {QueueName}.",
            rabbitMqOptions.NormalizedQueueName);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        RabbitMqOptions rabbitMqOptions,
        CancellationToken cancellationToken)
    {
        IncidentProcessingQueueMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<IncidentProcessingQueueMessage>(
                delivery.Body.Span,
                JsonOptions);

            if (message is null || message.IncidentId == Guid.Empty)
            {
                logger.LogWarning("Rejecting invalid RabbitMQ incident processing message.");
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            await ProcessMessageAsync(message, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogInformation(
                "Processed RabbitMQ incident message {MessageId} for incident {IncidentId}.",
                message.MessageId,
                message.IncidentId);
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
        IncidentProcessingQueueMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IncidentProcessingPipeline>();
        var incident = await incidents.GetByIdAsync(message.IncidentId, cancellationToken)
            ?? throw new InvalidOperationException($"Incident {message.IncidentId} could not be found.");

        await pipeline.ProcessAsync(incident, message.Trigger, cancellationToken);
    }

    private async Task HandleFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        RabbitMqOptions rabbitMqOptions,
        IncidentProcessingQueueMessage? message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (message is null)
        {
            logger.LogError(exception, "Could not deserialize RabbitMQ incident processing message.");
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
                    rabbitMqOptions.NormalizedRetryExchangeName,
                    rabbitMqOptions.NormalizedRetryRoutingKey,
                    nextMessage,
                    cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

                logger.LogWarning(
                    exception,
                    "Incident processing message {MessageId} for incident {IncidentId} failed. Retry attempt {Attempt}/{MaxAttempts} queued.",
                    message.MessageId,
                    message.IncidentId,
                    nextMessage.Attempt,
                    rabbitMqOptions.NormalizedMaxRetryAttempts);
                return;
            }

            await PublishAsync(
                channel,
                rabbitMqOptions.NormalizedDeadLetterExchangeName,
                rabbitMqOptions.NormalizedDeadLetterRoutingKey,
                nextMessage,
                cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogError(
                exception,
                "Incident processing message {MessageId} for incident {IncidentId} exhausted retries and was dead-lettered.",
                message.MessageId,
                message.IncidentId);
        }
        catch (Exception publishException)
        {
            logger.LogError(
                publishException,
                "Could not publish retry or dead-letter message for incident {IncidentId}; requeueing original delivery.",
                message.IncidentId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static async Task PublishAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        IncidentProcessingQueueMessage message,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("N"),
            Persistent = true,
            Type = nameof(IncidentProcessingQueueMessage)
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
