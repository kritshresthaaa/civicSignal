namespace CivicSignal.Infrastructure.Messaging;

public sealed record DataImportJobQueueMessage(
    Guid MessageId,
    Guid JobId,
    string Source,
    int Attempt,
    DateTimeOffset EnqueuedAt,
    string? LastError = null)
{
    public static DataImportJobQueueMessage Create(Guid jobId, string source, DateTimeOffset enqueuedAt)
    {
        return new DataImportJobQueueMessage(
            Guid.NewGuid(),
            jobId,
            string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim(),
            0,
            enqueuedAt);
    }

    public DataImportJobQueueMessage ForRetry(string error)
    {
        return this with
        {
            MessageId = Guid.NewGuid(),
            Attempt = Attempt + 1,
            EnqueuedAt = DateTimeOffset.UtcNow,
            LastError = error
        };
    }
}
