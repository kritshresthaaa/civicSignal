using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Weather;

internal sealed class NationalWeatherServiceWeatherService(
    HttpClient httpClient,
    IApplicationCache cache,
    IClock clock,
    IOptions<WeatherOptions> options,
    ILogger<NationalWeatherServiceWeatherService> logger) : IWeatherService
{
    private const string ProviderName = "NationalWeatherService";

    public async Task<WeatherObservationResult> GetCurrentConditionsAsync(
        double latitude,
        double longitude,
        DateTimeOffset observedNear,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(latitude, longitude);
        var cached = await cache.GetAsync<WeatherObservationResult>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await FetchCurrentConditionsAsync(latitude, longitude, cancellationToken);
        await cache.SetAsync(cacheKey, result, options.Value.CacheDuration, cancellationToken);

        return result;
    }

    private async Task<WeatherObservationResult> FetchCurrentConditionsAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var point = FormatCoordinate(latitude, longitude);
            var pointResponse = await httpClient.GetAsync($"/points/{point}", cancellationToken);
            if (pointResponse.StatusCode is HttpStatusCode.NotFound)
            {
                return WeatherObservationResult.Unavailable(
                    ProviderName,
                    "National Weather Service does not provide point metadata for these coordinates.",
                    clock.UtcNow);
            }

            pointResponse.EnsureSuccessStatusCode();
            var pointPayload = await pointResponse.Content.ReadFromJsonAsync<NwsPointResponse>(
                cancellationToken: cancellationToken);
            var stationsUrl = pointPayload?.Properties?.ObservationStations;
            if (string.IsNullOrWhiteSpace(stationsUrl))
            {
                return WeatherObservationResult.Unavailable(
                    ProviderName,
                    "National Weather Service point metadata did not include observation stations.",
                    clock.UtcNow);
            }

            var stationId = await GetNearestStationIdentifierAsync(stationsUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(stationId))
            {
                return WeatherObservationResult.Unavailable(
                    ProviderName,
                    "No nearby National Weather Service observation station was found.",
                    clock.UtcNow);
            }

            var observation = await GetLatestObservationAsync(stationId, cancellationToken);
            var alertSummary = await GetAlertSummaryAsync(latitude, longitude, cancellationToken);

            return new WeatherObservationResult(
                true,
                ProviderName,
                clock.UtcNow,
                stationId,
                observation?.Properties?.TextDescription,
                observation?.Properties?.Temperature?.Value,
                observation?.Properties?.WindSpeed?.Value,
                FormatWindDirection(observation?.Properties?.WindDirection?.Value),
                observation?.Properties?.PrecipitationLastHour?.Value,
                alertSummary,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Weather lookup failed for {Latitude}, {Longitude}.",
                latitude,
                longitude);

            return WeatherObservationResult.Unavailable(
                ProviderName,
                "Weather provider request failed.",
                clock.UtcNow);
        }
    }

    private async Task<string?> GetNearestStationIdentifierAsync(
        string stationsUrl,
        CancellationToken cancellationToken)
    {
        var stations = await httpClient.GetFromJsonAsync<NwsStationsResponse>(
            MakeRelativePath(stationsUrl),
            cancellationToken);

        return stations?.Features?
            .Select(feature => feature.Properties?.StationIdentifier)
            .FirstOrDefault(identifier => !string.IsNullOrWhiteSpace(identifier));
    }

    private async Task<NwsObservationResponse?> GetLatestObservationAsync(
        string stationIdentifier,
        CancellationToken cancellationToken)
    {
        return await httpClient.GetFromJsonAsync<NwsObservationResponse>(
            $"/stations/{Uri.EscapeDataString(stationIdentifier)}/observations/latest",
            cancellationToken);
    }

    private async Task<string?> GetAlertSummaryAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var alerts = await httpClient.GetFromJsonAsync<NwsAlertsResponse>(
            $"/alerts/active?point={FormatCoordinate(latitude, longitude)}",
            cancellationToken);
        var activeAlerts = alerts?.Features?
            .Select(feature => feature.Properties)
            .Where(properties => !string.IsNullOrWhiteSpace(properties?.Event))
            .Take(3)
            .Select(properties => $"{properties!.Event} ({properties.Severity ?? "Unknown"})")
            .ToArray() ?? [];

        return activeAlerts.Length == 0
            ? "No active weather alerts."
            : string.Join("; ", activeAlerts);
    }

    private static string FormatCoordinate(double latitude, double longitude)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{latitude:0.####},{longitude:0.####}");
    }

    private static string? FormatWindDirection(double? degrees)
    {
        if (degrees is null)
        {
            return null;
        }

        var normalized = ((degrees.Value % 360) + 360) % 360;
        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var index = (int)Math.Round(normalized / 45, MidpointRounding.AwayFromZero) % directions.Length;
        return directions[index];
    }

    private static string MakeRelativePath(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return $"{uri.AbsolutePath}{uri.Query}";
        }

        return value.StartsWith("/", StringComparison.Ordinal) ? value : $"/{value}";
    }

    private static string BuildCacheKey(double latitude, double longitude)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"weather:nws:{Math.Round(latitude, 3):0.000}:{Math.Round(longitude, 3):0.000}");
    }

    private sealed class NwsPointResponse
    {
        [JsonPropertyName("properties")]
        public NwsPointProperties? Properties { get; init; }
    }

    private sealed class NwsPointProperties
    {
        [JsonPropertyName("observationStations")]
        public string? ObservationStations { get; init; }
    }

    private sealed class NwsStationsResponse
    {
        [JsonPropertyName("features")]
        public IReadOnlyCollection<NwsStationFeature>? Features { get; init; }
    }

    private sealed class NwsStationFeature
    {
        [JsonPropertyName("properties")]
        public NwsStationProperties? Properties { get; init; }
    }

    private sealed class NwsStationProperties
    {
        [JsonPropertyName("stationIdentifier")]
        public string? StationIdentifier { get; init; }
    }

    private sealed class NwsObservationResponse
    {
        [JsonPropertyName("properties")]
        public NwsObservationProperties? Properties { get; init; }
    }

    private sealed class NwsObservationProperties
    {
        [JsonPropertyName("textDescription")]
        public string? TextDescription { get; init; }

        [JsonPropertyName("temperature")]
        public NwsQuantitativeValue? Temperature { get; init; }

        [JsonPropertyName("windSpeed")]
        public NwsQuantitativeValue? WindSpeed { get; init; }

        [JsonPropertyName("windDirection")]
        public NwsQuantitativeValue? WindDirection { get; init; }

        [JsonPropertyName("precipitationLastHour")]
        public NwsQuantitativeValue? PrecipitationLastHour { get; init; }
    }

    private sealed class NwsQuantitativeValue
    {
        [JsonPropertyName("value")]
        public double? Value { get; init; }
    }

    private sealed class NwsAlertsResponse
    {
        [JsonPropertyName("features")]
        public IReadOnlyCollection<NwsAlertFeature>? Features { get; init; }
    }

    private sealed class NwsAlertFeature
    {
        [JsonPropertyName("properties")]
        public NwsAlertProperties? Properties { get; init; }
    }

    private sealed class NwsAlertProperties
    {
        [JsonPropertyName("event")]
        public string? Event { get; init; }

        [JsonPropertyName("severity")]
        public string? Severity { get; init; }
    }
}
