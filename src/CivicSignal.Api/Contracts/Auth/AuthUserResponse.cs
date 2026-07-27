using CivicSignal.Application.Identity;

namespace CivicSignal.Api.Contracts.Auth;

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyCollection<string> Roles)
{
    public static AuthUserResponse FromDto(AuthUserDto user)
    {
        return new AuthUserResponse(user.Id, user.Email, user.DisplayName, user.Roles);
    }
}
