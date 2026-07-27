namespace CivicSignal.Infrastructure.Ai;

internal sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:8010";

    public int TimeoutSeconds { get; set; } = 30;

    public bool UseRemoteEmbeddings { get; set; } = true;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 300));
}
