namespace CivicSignal.Infrastructure.Identity;

internal sealed class DevelopmentSeedUserOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool ResetPassword { get; set; } = true;

    public string[] Roles { get; set; } = [];
}
