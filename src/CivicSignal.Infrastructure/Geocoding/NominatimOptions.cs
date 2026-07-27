namespace CivicSignal.Infrastructure.Geocoding;

internal sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    public bool Enabled { get; set; } = false;

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    public string UserAgent { get; set; } = "CivicSignalAI/0.1 local-development";

    public int TimeoutSeconds { get; set; } = 15;

    public int CacheMinutes { get; set; } = 1440;

    public int SearchLimit { get; set; } = 6;

    public string CountryCodes { get; set; } = "us";

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 60));

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(Math.Clamp(CacheMinutes, 1, 10080));

    public int NormalizedSearchLimit => Math.Clamp(SearchLimit, 1, 10);
}
