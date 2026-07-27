namespace CivicSignal.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName);
