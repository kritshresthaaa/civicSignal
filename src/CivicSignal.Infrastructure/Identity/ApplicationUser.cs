using Microsoft.AspNetCore.Identity;

namespace CivicSignal.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
