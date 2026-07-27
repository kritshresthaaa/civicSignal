namespace CivicSignal.Worker.Options;

public sealed class IncidentProcessingWorkerOptions
{
    public const string SectionName = "Worker:IncidentProcessing";

    private static readonly string[] DefaultSteps =
    [
        "Geocoding",
        "MediaAnalysis",
        "DuplicateCheck",
        "TriageDraft",
        "ControlledAgentWorkflow"
    ];

    public bool Enabled { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 15;

    public int BatchSize { get; set; } = 10;

    public int StepDelayMilliseconds { get; set; } = 250;

    public string[] Steps { get; set; } = DefaultSteps;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(Math.Max(1, PollingIntervalSeconds));

    public TimeSpan StepDelay => TimeSpan.FromMilliseconds(Math.Max(0, StepDelayMilliseconds));

    public int NormalizedBatchSize => Math.Clamp(BatchSize, 1, 100);

    public IReadOnlyCollection<string> NormalizedSteps
    {
        get
        {
            var configuredSteps = Steps
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .Select(step => step.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return configuredSteps.Length > 0 ? configuredSteps : DefaultSteps;
        }
    }
}
