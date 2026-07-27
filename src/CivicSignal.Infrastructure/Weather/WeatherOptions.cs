namespace CivicSignal.Infrastructure.Weather;

internal sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    public bool Enabled { get; set; } = false;

    public string Provider { get; set; } = "NationalWeatherService";

    public string BaseUrl { get; set; } = "https://api.weather.gov";

    public string UserAgent { get; set; } = "CivicSignalAI/0.1 local-development";

    public int TimeoutSeconds { get; set; } = 15;

    public int CacheMinutes { get; set; } = 20;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 60));

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(Math.Clamp(CacheMinutes, 1, 240));
}
