using CivicSignal.Api.Contracts.Auth;
using CivicSignal.Api.Security;
using CivicSignal.Application.Identity;
using CivicSignal.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CivicSignal.Api.Controllers;

[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Auth)]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService auth,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthUserResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await auth.RegisterAsync(
            new RegisterUserInput(request.Email, request.Password, request.DisplayName),
            cancellationToken);

        return CreatedAtAction(nameof(Me), AuthUserResponse.FromDto(user));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var token = await auth.LoginAsync(new LoginInput(request.Email, request.Password), cancellationToken);
        if (token is null)
        {
            return Unauthorized();
        }

        AppendAuthCookies(token);
        return Ok(AuthTokenResponse.FromDto(token));
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = GetRefreshToken(request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var token = await auth.RefreshAsync(refreshToken, cancellationToken);
        if (token is null)
        {
            DeleteAuthCookies();
            return Unauthorized();
        }

        AppendAuthCookies(token);
        return Ok(AuthTokenResponse.FromDto(token));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = GetRefreshToken(request);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await auth.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        DeleteAuthCookies();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await auth.GetCurrentUserAsync(User, cancellationToken);
        return user is null ? Unauthorized() : Ok(AuthUserResponse.FromDto(user));
    }

    private string? GetRefreshToken(RefreshTokenRequest? request)
    {
        return !string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies.TryGetValue(jwtOptions.Value.RefreshTokenCookieName, out var refreshToken)
                ? refreshToken
                : null;
    }

    private void AppendAuthCookies(AuthTokenDto token)
    {
        Response.Cookies.Append(
            jwtOptions.Value.AccessTokenCookieName,
            token.AccessToken,
            CreateCookieOptions(token.AccessTokenExpiresAt, path: "/"));
        Response.Cookies.Append(
            jwtOptions.Value.RefreshTokenCookieName,
            token.RefreshToken,
            CreateCookieOptions(token.RefreshTokenExpiresAt, path: "/api/auth"));
    }

    private void DeleteAuthCookies()
    {
        Response.Cookies.Delete(jwtOptions.Value.AccessTokenCookieName, CreateDeleteCookieOptions(path: "/"));
        Response.Cookies.Delete(jwtOptions.Value.RefreshTokenCookieName, CreateDeleteCookieOptions(path: "/api/auth"));
    }

    private CookieOptions CreateCookieOptions(DateTimeOffset expiresAt, string path)
    {
        return new CookieOptions
        {
            Expires = expiresAt,
            HttpOnly = true,
            IsEssential = true,
            Path = path,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps
        };
    }

    private CookieOptions CreateDeleteCookieOptions(string path)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = path,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps
        };
    }

}
