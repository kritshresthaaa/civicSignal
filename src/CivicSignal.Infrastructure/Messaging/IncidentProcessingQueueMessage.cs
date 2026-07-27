namespace CivicSignal.Infrastructure.Messaging;

public sealed record IncidentProcessingQueueMessage(
    Guid MessageId,
    Guid IncidentId,
    string Trigger,
    int Attempt,
    DateTimeOffset EnqueuedAt,
    string? LastError = null)
{
    public static IncidentProcessingQueueMessage Create(Guid incidentId, string trigger, DateTimeOffset enqueuedAt)
    {
        return new IncidentProcessingQueueMessage(
            Guid.NewGuid(),
            incidentId,
            string.IsNullOrWhiteSpace(trigger) ? "Unknown" : trigger.Trim(),
            0,
            enqueuedAt);
    }

    public IncidentProcessingQueueMessage ForRetry(string error)
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
