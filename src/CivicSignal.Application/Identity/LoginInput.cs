namespace CivicSignal.Application.Identity;

public sealed record LoginInput(
    string Email,
    string Password);
