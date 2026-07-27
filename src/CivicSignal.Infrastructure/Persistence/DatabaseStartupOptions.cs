namespace CivicSignal.Infrastructure.Persistence;

internal sealed class DatabaseStartupOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; set; }

    public bool DevelopmentOnly { get; set; } = true;
}
