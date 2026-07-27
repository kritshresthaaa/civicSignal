using System.Security.Claims;

namespace CivicSignal.Application.Identity;

public interface IAuthService
{
    Task<AuthUserDto> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default);

    Task<AuthTokenDto?> LoginAsync(LoginInput input, CancellationToken cancellationToken = default);

    Task<AuthTokenDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<AuthUserDto?> ValidateCredentialsAsync(LoginInput input, CancellationToken cancellationToken = default);

    Task<AuthUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
