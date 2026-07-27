namespace CivicSignal.Application.Forecasting;

public sealed record IncidentForecastInput(
    int HistoryDays = 30,
    int HorizonDays = 7,
    string? Category = null,
    string? AgencyCode = null);
