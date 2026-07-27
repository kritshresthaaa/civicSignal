using System.Text.Json;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CivicSignal.Infrastructure.Messaging;

internal sealed class RabbitMqIncidentProcessingQueue(
    IOptions<RabbitMqOptions> options,
    IClock clock,
    ILogger<RabbitMqIncidentProcessingQueue> logger) : IIncidentProcessingQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(
        Guid incidentId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        var rabbitMqOptions = options.Value;
        var message = IncidentProcessingQueueMessage.Create(incidentId, trigger, clock.UtcNow);
        var connectionFactory = RabbitMqTopology.CreateConnectionFactory(rabbitMqOptions);

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, rabbitMqOptions, cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("N"),
            Persistent = true,
            Type = nameof(IncidentProcessingQueueMessage)
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await channel.BasicPublishAsync(
            exchange: rabbitMqOptions.NormalizedExchangeName,
            routingKey: rabbitMqOptions.NormalizedRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Queued incident {IncidentId} for processing with RabbitMQ message {MessageId}.",
            incidentId,
            message.MessageId);
    }
}
