namespace CivicSignal.Domain.Incidents;

public sealed class IncidentFeedback
{
    private IncidentFeedback()
    {
    }

    private IncidentFeedback(Guid incidentId, int rating, string? comment, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Rating = rating;
        Comment = NormalizeOptional(comment, 2_000);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static IncidentFeedback Create(Guid incidentId, int rating, string? comment, DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Feedback rating must be between 1 and 5.");
        }

        return new IncidentFeedback(incidentId, rating, comment, createdAt);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Comment cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
