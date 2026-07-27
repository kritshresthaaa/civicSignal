namespace CivicSignal.Application.Identity;

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyCollection<string> Roles);
