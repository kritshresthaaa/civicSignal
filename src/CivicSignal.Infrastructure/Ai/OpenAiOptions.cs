namespace CivicSignal.Infrastructure.Ai;

internal sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";

    public string Model { get; set; } = "gpt-5.1";

    public int TimeoutSeconds { get; set; } = 60;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 5, 300));
}
