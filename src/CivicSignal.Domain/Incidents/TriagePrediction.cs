using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Incidents;

public sealed class TriagePrediction
{
    private readonly List<PredictionEvidence> _evidenceItems = [];

    private TriagePrediction()
    {
        Summary = string.Empty;
        ModelName = string.Empty;
    }

    private TriagePrediction(
        Guid incidentId,
        IncidentCategory category,
        IncidentSeverity severity,
        ConfidenceScore confidence,
        AgencyCode suggestedAgency,
        string summary,
        string modelName,
        string? modelVersion,
        string? promptVersion,
        long? processingTimeMilliseconds,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Category = category;
        Severity = severity;
        Confidence = confidence;
        SuggestedAgency = suggestedAgency;
        Summary = Normalize(summary, nameof(summary), 2_000);
        ModelName = Normalize(modelName, nameof(modelName), 160);
        ModelVersion = NormalizeOptional(modelVersion, nameof(modelVersion), 80);
        PromptVersion = NormalizeOptional(promptVersion, nameof(promptVersion), 80);
        ProcessingTimeMilliseconds = ValidateProcessingTime(processingTimeMilliseconds);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public IncidentCategory Category { get; private set; }

    public IncidentSeverity Severity { get; private set; }

    public ConfidenceScore Confidence { get; private set; }

    public AgencyCode SuggestedAgency { get; private set; }

    public string Summary { get; private set; }

    public string ModelName { get; private set; }

    public string? ModelVersion { get; private set; }

    public string? PromptVersion { get; private set; }

    public long? ProcessingTimeMilliseconds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<PredictionEvidence> EvidenceItems => _evidenceItems.AsReadOnly();

    internal static TriagePrediction Create(
        Guid incidentId,
        IncidentCategory category,
        IncidentSeverity severity,
        ConfidenceScore confidence,
        AgencyCode suggestedAgency,
        string summary,
        string modelName,
        string? modelVersion,
        string? promptVersion,
        long? processingTimeMilliseconds,
        DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        if (!Enum.IsDefined(typeof(IncidentSeverity), severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Severity is not supported.");
        }

        return new TriagePrediction(
            incidentId,
            category,
            severity,
            confidence,
            suggestedAgency,
            summary,
            modelName,
            modelVersion,
            promptVersion,
            processingTimeMilliseconds,
            createdAt);
    }

    public PredictionEvidence AddEvidence(
        string kind,
        string title,
        string detail,
        double? confidence,
        DateTimeOffset createdAt)
    {
        var evidence = PredictionEvidence.Create(Id, kind, title, detail, confidence, createdAt);
        _evidenceItems.Add(evidence);

        return evidence;
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

    private static string? NormalizeOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Normalize(value, parameterName, maxLength);
    }

    private static long? ValidateProcessingTime(long? processingTimeMilliseconds)
    {
        if (processingTimeMilliseconds is null)
        {
            return null;
        }

        if (processingTimeMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingTimeMilliseconds),
                processingTimeMilliseconds,
                "Processing time cannot be negative.");
        }

        return processingTimeMilliseconds;
    }
}
