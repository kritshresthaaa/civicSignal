using CivicSignal.Application.Forecasting.Models;

namespace CivicSignal.Application.Forecasting;

public interface IIncidentForecastingService
{
    Task<IncidentForecastDto> ForecastIncidentVolumeAsync(
        IncidentForecastInput input,
        CancellationToken cancellationToken = default);
}
