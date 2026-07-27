namespace CivicSignal.Infrastructure.DemoData;

internal sealed class DemoDataSeedOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; set; }

    public bool DevelopmentOnly { get; set; } = true;
}
