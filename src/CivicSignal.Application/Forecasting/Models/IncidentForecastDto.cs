namespace CivicSignal.Application.Forecasting.Models;

public sealed record IncidentForecastDto(
    DateOnly GeneratedOn,
    int HistoryDays,
    int HorizonDays,
    string Segment,
    string ModelName,
    string ModelVersion,
    IReadOnlyCollection<IncidentForecastPointDto> History,
    IReadOnlyCollection<IncidentForecastPointDto> Forecast,
    string Explanation);
