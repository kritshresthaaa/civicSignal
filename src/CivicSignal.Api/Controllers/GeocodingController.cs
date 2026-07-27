using CivicSignal.Api.Security;
using CivicSignal.Application.Abstractions.Geocoding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicSignal.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/geocoding")]
public sealed class GeocodingController(IGeocodingService geocoding) : ControllerBase
{
    [HttpGet("search")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<IReadOnlyCollection<GeocodingResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GeocodingResult>>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = "Search query must contain at least 3 characters."
            });
        }

        var results = await geocoding.SearchAsync(query, cancellationToken);

        return Ok(results);
    }

    [HttpGet("reverse")]
    [EnableRateLimiting(SecurityRateLimitPolicies.PublicWrite)]
    [ProducesResponseType<GeocodingResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeocodingResult>> Reverse(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = "Latitude must be between -90 and 90."
            });
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = "Longitude must be between -180 and 180."
            });
        }

        var result = await geocoding.ReverseAsync(latitude, longitude, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
