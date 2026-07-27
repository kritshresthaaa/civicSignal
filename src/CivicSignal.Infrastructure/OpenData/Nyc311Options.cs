namespace CivicSignal.Infrastructure.OpenData;

internal sealed class Nyc311Options
{
    public const string SectionName = "Nyc311";

    public string BaseUrl { get; set; } = "https://data.cityofnewyork.us";

    public string ResourcePath { get; set; } = "/resource/erm2-nwe9.json";

    public string? AppToken { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int DefaultLimit { get; set; } = 1_000;

    public int MaxLimit { get; set; } = 5_000;
}
