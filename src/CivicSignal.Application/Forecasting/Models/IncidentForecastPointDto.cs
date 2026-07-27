namespace CivicSignal.Application.Forecasting.Models;

public sealed record IncidentForecastPointDto(
    DateOnly Date,
    int? ActualCount,
    double ForecastCount,
    int LowerBound,
    int UpperBound);
