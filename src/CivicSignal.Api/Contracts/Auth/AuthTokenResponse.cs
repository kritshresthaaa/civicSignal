using CivicSignal.Application.Identity;

namespace CivicSignal.Api.Contracts.Auth;

public sealed record AuthTokenResponse(
    string TokenType,
    string AccessToken,
    long ExpiresIn,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    long RefreshTokenExpiresIn,
    DateTimeOffset RefreshTokenExpiresAt)
{
    public static AuthTokenResponse FromDto(AuthTokenDto token)
    {
        return new AuthTokenResponse(
            token.TokenType,
            token.AccessToken,
            token.ExpiresIn,
            token.AccessTokenExpiresAt,
            token.RefreshToken,
            token.RefreshTokenExpiresIn,
            token.RefreshTokenExpiresAt);
    }
}
