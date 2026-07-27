namespace CivicSignal.Domain.Incidents;

public sealed class IncidentUpdateRequest
{
    private IncidentUpdateRequest()
    {
        Message = string.Empty;
    }

    private IncidentUpdateRequest(Guid incidentId, string message, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Message = NormalizeRequired(message, 2_000);
        Status = IncidentUpdateRequestStatus.Open;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public string Message { get; private set; }

    public IncidentUpdateRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static IncidentUpdateRequest Create(Guid incidentId, string message, DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        return new IncidentUpdateRequest(incidentId, message, createdAt);
    }

    private static string NormalizeRequired(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Message is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Message cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
