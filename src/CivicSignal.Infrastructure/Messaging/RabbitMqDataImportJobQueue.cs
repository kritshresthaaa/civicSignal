using System.Text.Json;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CivicSignal.Infrastructure.Messaging;

internal sealed class RabbitMqDataImportJobQueue(
    IOptions<RabbitMqOptions> options,
    IClock clock,
    ILogger<RabbitMqDataImportJobQueue> logger) : IDataImportJobQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(
        Guid jobId,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Data import job id is required.", nameof(jobId));
        }

        var rabbitMqOptions = options.Value;
        var message = DataImportJobQueueMessage.Create(jobId, source, clock.UtcNow);
        var connectionFactory = RabbitMqTopology.CreateConnectionFactory(rabbitMqOptions);

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, rabbitMqOptions, cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("N"),
            Persistent = true,
            Type = nameof(DataImportJobQueueMessage)
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await channel.BasicPublishAsync(
            exchange: rabbitMqOptions.NormalizedDataImportExchangeName,
            routingKey: rabbitMqOptions.NormalizedDataImportRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Queued data import job {JobId} with RabbitMQ message {MessageId}.",
            jobId,
            message.MessageId);
    }
}
