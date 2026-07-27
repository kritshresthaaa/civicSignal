namespace CivicSignal.Worker.Options;

public sealed class DataImportWorkerOptions
{
    public const string SectionName = "Worker:DataImports";

    public bool Enabled { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 20;

    public int BatchSize { get; set; } = 2;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(Math.Max(1, PollingIntervalSeconds));

    public int NormalizedBatchSize => Math.Clamp(BatchSize, 1, 20);
}
