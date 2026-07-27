namespace CivicSignal.Infrastructure.Identity;

public sealed class ApplicationRefreshToken
{
    private ApplicationRefreshToken()
    {
        TokenHash = string.Empty;
    }

    private ApplicationRefreshToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public ApplicationUser? User { get; private set; }

    public static ApplicationRefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Refresh token hash is required.", nameof(tokenHash));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Refresh token expiry must be after creation time.", nameof(expiresAt));
        }

        return new ApplicationRefreshToken(userId, tokenHash, createdAt, expiresAt);
    }

    public bool IsActive(DateTimeOffset utcNow)
    {
        return RevokedAt is null && ExpiresAt > utcNow;
    }

    public void Revoke(DateTimeOffset revokedAt, string? replacedByTokenHash = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = revokedAt;
        ReplacedByTokenHash = string.IsNullOrWhiteSpace(replacedByTokenHash)
            ? null
            : replacedByTokenHash;
    }
}
