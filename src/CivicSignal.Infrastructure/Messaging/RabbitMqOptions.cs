namespace CivicSignal.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "civicsignal.incidents";

    public string QueueName { get; set; } = "civicsignal.incidents.processing";

    public string RoutingKey { get; set; } = "incident.submitted";

    public string RetryExchangeName { get; set; } = "civicsignal.incidents.retry";

    public string RetryQueueName { get; set; } = "civicsignal.incidents.retry";

    public string RetryRoutingKey { get; set; } = "incident.retry";

    public string DeadLetterExchangeName { get; set; } = "civicsignal.incidents.dead";

    public string DeadLetterQueueName { get; set; } = "civicsignal.incidents.dead";

    public string DeadLetterRoutingKey { get; set; } = "incident.dead";

    public string DataImportExchangeName { get; set; } = "civicsignal.data-imports";

    public string DataImportQueueName { get; set; } = "civicsignal.data-imports.processing";

    public string DataImportRoutingKey { get; set; } = "data-import.requested";

    public string DataImportRetryExchangeName { get; set; } = "civicsignal.data-imports.retry";

    public string DataImportRetryQueueName { get; set; } = "civicsignal.data-imports.retry";

    public string DataImportRetryRoutingKey { get; set; } = "data-import.retry";

    public string DataImportDeadLetterExchangeName { get; set; } = "civicsignal.data-imports.dead";

    public string DataImportDeadLetterQueueName { get; set; } = "civicsignal.data-imports.dead";

    public string DataImportDeadLetterRoutingKey { get; set; } = "data-import.dead";

    public int RetryDelaySeconds { get; set; } = 15;

    public int MaxRetryAttempts { get; set; } = 3;

    public ushort PrefetchCount { get; set; } = 4;

    public string NormalizedHostName => Normalize(HostName, "localhost");

    public int NormalizedPort => Port <= 0 ? 5672 : Port;

    public string NormalizedUserName => Normalize(UserName, "guest");

    public string NormalizedPassword => Normalize(Password, "guest");

    public string NormalizedVirtualHost => Normalize(VirtualHost, "/");

    public string NormalizedExchangeName => Normalize(ExchangeName, "civicsignal.incidents");

    public string NormalizedQueueName => Normalize(QueueName, "civicsignal.incidents.processing");

    public string NormalizedRoutingKey => Normalize(RoutingKey, "incident.submitted");

    public string NormalizedRetryExchangeName => Normalize(RetryExchangeName, "civicsignal.incidents.retry");

    public string NormalizedRetryQueueName => Normalize(RetryQueueName, "civicsignal.incidents.retry");

    public string NormalizedRetryRoutingKey => Normalize(RetryRoutingKey, "incident.retry");

    public string NormalizedDeadLetterExchangeName => Normalize(DeadLetterExchangeName, "civicsignal.incidents.dead");

    public string NormalizedDeadLetterQueueName => Normalize(DeadLetterQueueName, "civicsignal.incidents.dead");

    public string NormalizedDeadLetterRoutingKey => Normalize(DeadLetterRoutingKey, "incident.dead");

    public string NormalizedDataImportExchangeName => Normalize(DataImportExchangeName, "civicsignal.data-imports");

    public string NormalizedDataImportQueueName => Normalize(DataImportQueueName, "civicsignal.data-imports.processing");

    public string NormalizedDataImportRoutingKey => Normalize(DataImportRoutingKey, "data-import.requested");

    public string NormalizedDataImportRetryExchangeName => Normalize(DataImportRetryExchangeName, "civicsignal.data-imports.retry");

    public string NormalizedDataImportRetryQueueName => Normalize(DataImportRetryQueueName, "civicsignal.data-imports.retry");

    public string NormalizedDataImportRetryRoutingKey => Normalize(DataImportRetryRoutingKey, "data-import.retry");

    public string NormalizedDataImportDeadLetterExchangeName => Normalize(DataImportDeadLetterExchangeName, "civicsignal.data-imports.dead");

    public string NormalizedDataImportDeadLetterQueueName => Normalize(DataImportDeadLetterQueueName, "civicsignal.data-imports.dead");

    public string NormalizedDataImportDeadLetterRoutingKey => Normalize(DataImportDeadLetterRoutingKey, "data-import.dead");

    public int NormalizedRetryDelayMilliseconds => Math.Clamp(RetryDelaySeconds, 1, 3_600) * 1_000;

    public int NormalizedMaxRetryAttempts => Math.Clamp(MaxRetryAttempts, 0, 25);

    public ushort NormalizedPrefetchCount => PrefetchCount == 0 ? (ushort)1 : PrefetchCount;

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
