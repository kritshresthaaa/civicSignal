namespace CivicSignal.Domain.Incidents;

public sealed class IncidentMedia
{
    private IncidentMedia()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        StorageUri = string.Empty;
        AnalysisStatus = IncidentMediaAnalysisStatus.Pending;
    }

    private IncidentMedia(
        Guid incidentId,
        string fileName,
        string contentType,
        string storageUri,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        FileName = Normalize(fileName, nameof(fileName), 260);
        ContentType = Normalize(contentType, nameof(contentType), 160).ToLowerInvariant();
        StorageUri = Normalize(storageUri, nameof(storageUri), 2_048);
        MediaType = DetermineMediaType(ContentType);
        AnalysisStatus = IncidentMediaAnalysisStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public string StorageUri { get; private set; }

    public IncidentMediaType MediaType { get; private set; }

    public IncidentMediaAnalysisStatus AnalysisStatus { get; private set; }

    public string? AnalysisSummary { get; private set; }

    public string? Transcript { get; private set; }

    public string? DetectedLabels { get; private set; }

    public double? AnalysisConfidence { get; private set; }

    public string? AnalysisModelName { get; private set; }

    public string? AnalysisModelVersion { get; private set; }

    public long? AnalysisProcessingTimeMilliseconds { get; private set; }

    public string? AnalysisError { get; private set; }

    public DateTimeOffset? AnalyzedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static IncidentMedia Create(
        Guid incidentId,
        string fileName,
        string contentType,
        string storageUri,
        DateTimeOffset createdAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        return new IncidentMedia(incidentId, fileName, contentType, storageUri, createdAt);
    }

    public void StartAnalysis(DateTimeOffset startedAt)
    {
        AnalysisStatus = IncidentMediaAnalysisStatus.InProgress;
        AnalysisError = null;
        AnalyzedAt = startedAt;
    }

    public void CompleteAnalysis(
        string summary,
        string? transcript,
        IReadOnlyCollection<string> detectedLabels,
        double? confidence,
        string modelName,
        string? modelVersion,
        long? processingTimeMilliseconds,
        DateTimeOffset analyzedAt)
    {
        AnalysisStatus = IncidentMediaAnalysisStatus.Succeeded;
        AnalysisSummary = Normalize(summary, nameof(summary), 1_000);
        Transcript = NormalizeOptional(transcript, nameof(transcript), 4_000);
        DetectedLabels = NormalizeOptional(
            string.Join(
                ", ",
                detectedLabels
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Select(label => label.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)),
            nameof(detectedLabels),
            1_000);
        AnalysisConfidence = ValidateConfidence(confidence);
        AnalysisModelName = Normalize(modelName, nameof(modelName), 160);
        AnalysisModelVersion = NormalizeOptional(modelVersion, nameof(modelVersion), 80);
        AnalysisProcessingTimeMilliseconds = ValidateProcessingTime(processingTimeMilliseconds);
        AnalysisError = null;
        AnalyzedAt = analyzedAt;
    }

    public void FailAnalysis(string errorMessage, DateTimeOffset failedAt)
    {
        AnalysisStatus = IncidentMediaAnalysisStatus.Failed;
        AnalysisError = Normalize(errorMessage, nameof(errorMessage), 1_000);
        AnalyzedAt = failedAt;
    }

    public void SkipAnalysis(string reason, DateTimeOffset skippedAt)
    {
        AnalysisStatus = IncidentMediaAnalysisStatus.Skipped;
        AnalysisSummary = Normalize(reason, nameof(reason), 1_000);
        AnalysisError = null;
        AnalyzedAt = skippedAt;
    }

    private static IncidentMediaType DetermineMediaType(string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return IncidentMediaType.Image;
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return IncidentMediaType.Video;
        }

        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return IncidentMediaType.Audio;
        }

        return IncidentMediaType.Other;
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
        return string.IsNullOrWhiteSpace(value) ? null : Normalize(value, parameterName, maxLength);
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
