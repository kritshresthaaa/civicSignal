namespace CivicSignal.Application.Identity;

public sealed record RegisterUserInput(
    string Email,
    string Password,
    string? DisplayName);
