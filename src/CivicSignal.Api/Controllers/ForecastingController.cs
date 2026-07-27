using CivicSignal.Application.Forecasting;
using CivicSignal.Application.Forecasting.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicSignal.Api.Controllers;

[ApiController]
[Route("api/forecasting")]
public sealed class ForecastingController(IIncidentForecastingService forecasting) : ControllerBase
{
    [HttpGet("incident-volume")]
    [ProducesResponseType<IncidentForecastDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentForecastDto>> GetIncidentVolumeForecast(
        [FromQuery] int historyDays = 30,
        [FromQuery] int horizonDays = 7,
        [FromQuery] string? category = null,
        [FromQuery] string? agencyCode = null,
        CancellationToken cancellationToken = default)
    {
        var forecast = await forecasting.ForecastIncidentVolumeAsync(
            new IncidentForecastInput(historyDays, horizonDays, category, agencyCode),
            cancellationToken);

        return Ok(forecast);
    }
}
