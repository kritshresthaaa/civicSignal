namespace CivicSignal.Domain.DataImports;

public sealed class DataImportJob
{
    public const string Nyc311Source = "NYC311";
    public const string HistoricalComplaintsImportType = "HistoricalComplaints";

    private DataImportJob()
    {
        Source = string.Empty;
        ImportType = string.Empty;
        ParametersJson = "{}";
    }

    private DataImportJob(
        string source,
        string importType,
        string parametersJson,
        Guid? requestedByUserId,
        DateTimeOffset requestedAt)
    {
        Id = Guid.NewGuid();
        Source = NormalizeRequired(source, nameof(source), 40);
        ImportType = NormalizeRequired(importType, nameof(importType), 80);
        ParametersJson = NormalizeRequired(parametersJson, nameof(parametersJson), 4_000);
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        UpdatedAt = requestedAt;
        Status = DataImportJobStatus.Pending;
    }

    public Guid Id { get; private set; }

    public string Source { get; private set; }

    public string ImportType { get; private set; }

    public string ParametersJson { get; private set; }

    public DataImportJobStatus Status { get; private set; }

    public Guid? RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public int ReceivedCount { get; private set; }

    public int CreatedCount { get; private set; }

    public int UpdatedCount { get; private set; }

    public int SkippedCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DataImportJob RequestNyc311HistoricalComplaints(
        string parametersJson,
        Guid? requestedByUserId,
        DateTimeOffset requestedAt)
    {
        return new DataImportJob(
            Nyc311Source,
            HistoricalComplaintsImportType,
            parametersJson,
            requestedByUserId,
            requestedAt);
    }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status is not DataImportJobStatus.Pending and not DataImportJobStatus.Failed)
        {
            throw new InvalidOperationException("Only pending or failed import jobs can be started.");
        }

        Status = DataImportJobStatus.Running;
        StartedAt = startedAt;
        FinishedAt = null;
        ErrorMessage = null;
        UpdatedAt = startedAt;
    }

    public void Complete(
        int receivedCount,
        int createdCount,
        int updatedCount,
        int skippedCount,
        DateTimeOffset finishedAt)
    {
        if (Status is not DataImportJobStatus.Running)
        {
            throw new InvalidOperationException("Only running import jobs can be completed.");
        }

        ReceivedCount = EnsureNonNegative(receivedCount, nameof(receivedCount));
        CreatedCount = EnsureNonNegative(createdCount, nameof(createdCount));
        UpdatedCount = EnsureNonNegative(updatedCount, nameof(updatedCount));
        SkippedCount = EnsureNonNegative(skippedCount, nameof(skippedCount));
        Status = DataImportJobStatus.Succeeded;
        FinishedAt = finishedAt;
        ErrorMessage = null;
        UpdatedAt = finishedAt;
    }

    public void Fail(string errorMessage, DateTimeOffset failedAt)
    {
        if (Status is not DataImportJobStatus.Running)
        {
            throw new InvalidOperationException("Only running import jobs can fail.");
        }

        Status = DataImportJobStatus.Failed;
        FinishedAt = failedAt;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Import failed."
            : errorMessage.Trim()[..Math.Min(errorMessage.Trim().Length, 1_000)];
        UpdatedAt = failedAt;
    }

    public void Retry(DateTimeOffset requestedAt)
    {
        if (Status is not DataImportJobStatus.Failed)
        {
            throw new InvalidOperationException("Only failed import jobs can be retried.");
        }

        Status = DataImportJobStatus.Pending;
        StartedAt = null;
        FinishedAt = null;
        ErrorMessage = null;
        ReceivedCount = 0;
        CreatedCount = 0;
        UpdatedCount = 0;
        SkippedCount = 0;
        UpdatedAt = requestedAt;
    }

    private static int EnsureNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Import counts cannot be negative.");
        }

        return value;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }
}
