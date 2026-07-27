namespace CivicSignal.Infrastructure.Caching;

internal sealed class CivicSignalRedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = "localhost:6379";

    public string InstanceName { get; set; } = "CivicSignal:";
}
