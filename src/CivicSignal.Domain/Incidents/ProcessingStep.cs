namespace CivicSignal.Domain.Incidents;

public sealed class ProcessingStep
{
    private ProcessingStep()
    {
        Name = string.Empty;
    }

    private ProcessingStep(Guid incidentId, string name, DateTimeOffset startedAt)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Name = NormalizeName(name);
        Status = ProcessingStepStatus.InProgress;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public string Name { get; private set; }

    public ProcessingStepStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ProcessingStep Start(Guid incidentId, string name, DateTimeOffset startedAt)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        return new ProcessingStep(incidentId, name, startedAt);
    }

    internal void Start(DateTimeOffset startedAt)
    {
        if (Status is ProcessingStepStatus.InProgress)
        {
            throw new InvalidOperationException("Processing step is already in progress.");
        }

        Status = ProcessingStepStatus.InProgress;
        StartedAt = startedAt;
        CompletedAt = null;
        ErrorMessage = null;
        UpdatedAt = startedAt;
    }

    internal void Complete(DateTimeOffset completedAt)
    {
        EnsureInProgress(completedAt);

        Status = ProcessingStepStatus.Succeeded;
        CompletedAt = completedAt;
        ErrorMessage = null;
        UpdatedAt = completedAt;
    }

    internal void Fail(string errorMessage, DateTimeOffset failedAt)
    {
        EnsureInProgress(failedAt);

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message is required for failed processing steps.", nameof(errorMessage));
        }

        Status = ProcessingStepStatus.Failed;
        CompletedAt = failedAt;
        ErrorMessage = errorMessage.Trim();
        UpdatedAt = failedAt;
    }

    private void EnsureInProgress(DateTimeOffset completedAt)
    {
        if (Status is not ProcessingStepStatus.InProgress)
        {
            throw new InvalidOperationException("Processing step must be in progress.");
        }

        if (StartedAt is not null && completedAt < StartedAt)
        {
            throw new ArgumentException("Processing step completion cannot be before it started.", nameof(completedAt));
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Processing step name is required.", nameof(name));
        }

        var normalized = name.Trim();
        if (normalized.Length > 160)
        {
            throw new ArgumentException("Processing step name cannot exceed 160 characters.", nameof(name));
        }

        return normalized;
    }
}
