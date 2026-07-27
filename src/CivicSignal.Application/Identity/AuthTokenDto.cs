namespace CivicSignal.Application.Identity;

public sealed record AuthTokenDto(
    string TokenType,
    string AccessToken,
    long ExpiresIn,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    long RefreshTokenExpiresIn,
    DateTimeOffset RefreshTokenExpiresAt);
