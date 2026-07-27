using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Common;
using CivicSignal.Application.Forecasting.Models;
using CivicSignal.Domain.Incidents;
using FluentValidation;

namespace CivicSignal.Application.Forecasting;

internal sealed class IncidentForecastingService(
    IIncidentRepository incidents,
    IApplicationCache cache,
    IClock clock,
    IValidator<IncidentForecastInput> validator) : IIncidentForecastingService
{
    private const string ModelName = "moving-average-trend-baseline";
    private const string ModelVersion = "0.1.0";
    private static readonly TimeSpan ForecastCacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IncidentForecastDto> ForecastIncidentVolumeAsync(
        IncidentForecastInput input,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var cacheKey = BuildCacheKey(input, today);
        var cached = await cache.GetAsync<IncidentForecastDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var allIncidents = await incidents.ListAsync(cancellationToken);
        var startDate = today.AddDays(-(input.HistoryDays - 1));
        var filteredIncidents = allIncidents
            .Where(incident => DateOnly.FromDateTime(incident.CreatedAt.UtcDateTime) >= startDate)
            .Where(incident => MatchesSegment(incident, input))
            .ToArray();

        var countsByDate = filteredIncidents
            .GroupBy(incident => DateOnly.FromDateTime(incident.CreatedAt.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());
        var historyCounts = Enumerable
            .Range(0, input.HistoryDays)
            .Select(offset => countsByDate.GetValueOrDefault(startDate.AddDays(offset)))
            .ToArray();
        var baselineWindow = Math.Min(7, historyCounts.Length);
        var recentAverage = AverageLast(historyCounts, baselineWindow);
        var previousAverage = AveragePrevious(historyCounts, baselineWindow);
        var dailyTrend = (recentAverage - previousAverage) / Math.Max(1, baselineWindow);
        var dayOfWeekFactors = BuildDayOfWeekFactors(filteredIncidents, recentAverage);

        var history = Enumerable
            .Range(0, input.HistoryDays)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                var actualCount = countsByDate.GetValueOrDefault(date);
                return new IncidentForecastPointDto(
                    date,
                    actualCount,
                    actualCount,
                    actualCount,
                    actualCount);
            })
            .ToArray();
        var forecast = Enumerable
            .Range(1, input.HorizonDays)
            .Select(offset =>
            {
                var date = today.AddDays(offset);
                var dayFactor = dayOfWeekFactors.GetValueOrDefault(date.DayOfWeek, 1);
                var projected = Math.Max(0, (recentAverage + dailyTrend * offset) * dayFactor);
                var rounded = Math.Round(projected, 1);
                var margin = Math.Max(1, projected * 0.35);

                return new IncidentForecastPointDto(
                    date,
                    null,
                    rounded,
                    Math.Max(0, (int)Math.Floor(projected - margin)),
                    (int)Math.Ceiling(projected + margin));
            })
            .ToArray();

        var result = new IncidentForecastDto(
            today,
            input.HistoryDays,
            input.HorizonDays,
            BuildSegmentLabel(input),
            ModelName,
            ModelVersion,
            history,
            forecast,
            "Baseline forecast uses recent daily incident volume, short-term trend, and day-of-week seasonality. It is intended as a measurable starter model before a trained time-series model is added.");

        await cache.SetAsync(cacheKey, result, ForecastCacheDuration, cancellationToken);
        return result;
    }

    private static bool MatchesSegment(Incident incident, IncidentForecastInput input)
    {
        var category = LatestCategory(incident);
        var agency = LatestAgencyCode(incident);

        return (string.IsNullOrWhiteSpace(input.Category)
                || string.Equals(category, input.Category.Trim(), StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(input.AgencyCode)
                || string.Equals(agency, input.AgencyCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? LatestCategory(Incident incident)
    {
        return !string.IsNullOrWhiteSpace(incident.CorrectedCategory)
            ? incident.CorrectedCategory
            : incident.TriagePredictions
                .OrderByDescending(prediction => prediction.CreatedAt)
                .FirstOrDefault()
                ?.Category.Value;
    }

    private static string? LatestAgencyCode(Incident incident)
    {
        return !string.IsNullOrWhiteSpace(incident.CorrectedAgencyCode)
            ? incident.CorrectedAgencyCode
            : incident.TriagePredictions
                .OrderByDescending(prediction => prediction.CreatedAt)
                .FirstOrDefault()
                ?.SuggestedAgency.Value;
    }

    private static double AverageLast(int[] values, int window)
    {
        return values.Length == 0 ? 0 : values.TakeLast(window).DefaultIfEmpty(0).Average();
    }

    private static double AveragePrevious(int[] values, int window)
    {
        if (values.Length <= window)
        {
            return AverageLast(values, window);
        }

        return values
            .Take(values.Length - window)
            .TakeLast(window)
            .DefaultIfEmpty(0)
            .Average();
    }

    private static Dictionary<DayOfWeek, double> BuildDayOfWeekFactors(
        IReadOnlyCollection<Incident> incidents,
        double fallbackAverage)
    {
        var totalAverage = Math.Max(1, fallbackAverage);

        return Enum.GetValues<DayOfWeek>()
            .ToDictionary(
                day => day,
                day =>
                {
                    var counts = incidents
                        .Where(incident => incident.CreatedAt.DayOfWeek == day)
                        .GroupBy(incident => DateOnly.FromDateTime(incident.CreatedAt.UtcDateTime))
                        .Select(group => group.Count())
                        .ToArray();

                    if (counts.Length == 0)
                    {
                        return 1;
                    }

                    return Math.Clamp(counts.Average() / totalAverage, 0.55, 1.65);
                });
    }

    private static string BuildSegmentLabel(IncidentForecastInput input)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(input.Category) ? null : $"category={input.Category.Trim()}",
            string.IsNullOrWhiteSpace(input.AgencyCode) ? null : $"agency={input.AgencyCode.Trim().ToUpperInvariant()}"
        }.Where(part => part is not null);

        var segment = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(segment) ? "all incidents" : segment;
    }

    private static string BuildCacheKey(IncidentForecastInput input, DateOnly today)
    {
        return string.Join(
            ":",
            "forecast",
            "incident-volume",
            today.ToString("yyyyMMdd"),
            input.HistoryDays,
            input.HorizonDays,
            NormalizeCachePart(input.Category),
            NormalizeCachePart(input.AgencyCode));
    }

    private static string NormalizeCachePart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "all"
            : value.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}
