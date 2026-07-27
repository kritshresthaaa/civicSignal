using RabbitMQ.Client;

namespace CivicSignal.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options)
    {
        return new ConnectionFactory
        {
            HostName = options.NormalizedHostName,
            Port = options.NormalizedPort,
            UserName = options.NormalizedUserName,
            Password = options.NormalizedPassword,
            VirtualHost = options.NormalizedVirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
    }

    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken = default)
    {
        await DeclareRouteAsync(
            channel,
            options.NormalizedExchangeName,
            options.NormalizedQueueName,
            options.NormalizedRoutingKey,
            options.NormalizedRetryExchangeName,
            options.NormalizedRetryQueueName,
            options.NormalizedRetryRoutingKey,
            options.NormalizedDeadLetterExchangeName,
            options.NormalizedDeadLetterQueueName,
            options.NormalizedDeadLetterRoutingKey,
            options.NormalizedRetryDelayMilliseconds,
            cancellationToken);

        await DeclareRouteAsync(
            channel,
            options.NormalizedDataImportExchangeName,
            options.NormalizedDataImportQueueName,
            options.NormalizedDataImportRoutingKey,
            options.NormalizedDataImportRetryExchangeName,
            options.NormalizedDataImportRetryQueueName,
            options.NormalizedDataImportRetryRoutingKey,
            options.NormalizedDataImportDeadLetterExchangeName,
            options.NormalizedDataImportDeadLetterQueueName,
            options.NormalizedDataImportDeadLetterRoutingKey,
            options.NormalizedRetryDelayMilliseconds,
            cancellationToken);
    }

    private static async Task DeclareRouteAsync(
        IChannel channel,
        string exchangeName,
        string queueName,
        string routingKey,
        string retryExchangeName,
        string retryQueueName,
        string retryRoutingKey,
        string deadLetterExchangeName,
        string deadLetterQueueName,
        string deadLetterRoutingKey,
        int retryDelayMilliseconds,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            retryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            deadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var processingArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = deadLetterExchangeName,
            ["x-dead-letter-routing-key"] = deadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: processingArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queueName,
            exchangeName,
            routingKey,
            cancellationToken: cancellationToken);

        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = retryDelayMilliseconds,
            ["x-dead-letter-exchange"] = exchangeName,
            ["x-dead-letter-routing-key"] = routingKey
        };
        await channel.QueueDeclareAsync(
            retryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            retryQueueName,
            retryExchangeName,
            retryRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            deadLetterQueueName,
            deadLetterExchangeName,
            deadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
