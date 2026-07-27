namespace CivicSignal.Api.Contracts.Auth;

public sealed record RefreshTokenRequest(
    string? RefreshToken = null);
