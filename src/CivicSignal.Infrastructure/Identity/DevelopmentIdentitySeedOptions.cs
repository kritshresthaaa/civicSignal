namespace CivicSignal.Infrastructure.Identity;

internal sealed class DevelopmentIdentitySeedOptions
{
    public const string SectionName = "SeedUsers";

    public bool Enabled { get; set; }

    public bool DevelopmentOnly { get; set; } = true;

    public DevelopmentSeedUserOptions[] Users { get; set; } = [];
}
