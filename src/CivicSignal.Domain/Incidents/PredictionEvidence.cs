namespace CivicSignal.Domain.Incidents;

public sealed class PredictionEvidence
{
    private PredictionEvidence()
    {
        Kind = string.Empty;
        Title = string.Empty;
        Detail = string.Empty;
    }

    private PredictionEvidence(
        Guid triagePredictionId,
        string kind,
        string title,
        string detail,
        double? confidence,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        TriagePredictionId = triagePredictionId;
        Kind = Normalize(kind, nameof(kind), 64);
        Title = Normalize(title, nameof(title), 160);
        Detail = Normalize(detail, nameof(detail), 1_000);
        Confidence = ValidateConfidence(confidence);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TriagePredictionId { get; private set; }

    public string Kind { get; private set; }

    public string Title { get; private set; }

    public string Detail { get; private set; }

    public double? Confidence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static PredictionEvidence Create(
        Guid triagePredictionId,
        string kind,
        string title,
        string detail,
        double? confidence,
        DateTimeOffset createdAt)
    {
        if (triagePredictionId == Guid.Empty)
        {
            throw new ArgumentException("Triage prediction id is required.", nameof(triagePredictionId));
        }

        return new PredictionEvidence(triagePredictionId, kind, title, detail, confidence, createdAt);
    }

    private static string Normalize(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static double? ValidateConfidence(double? confidence)
    {
        if (confidence is null)
        {
            return null;
        }

        if (confidence < 0 || confidence > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0 and 1.");
        }

        return Math.Round(confidence.Value, 4);
    }
}
