using Microsoft.Extensions.Configuration;

namespace CivicSignal.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    private const string LocalDevelopmentSigningKey = "local-development-only-civic-signal-jwt-signing-key-change-me-2026";

    public string Issuer { get; set; } = "CivicSignal.Api";

    public string Audience { get; set; } = "CivicSignal.Client";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    public string AccessTokenCookieName { get; set; } = "civicsignal_access_token";

    public string RefreshTokenCookieName { get; set; } = "civicsignal_refresh_token";

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(Math.Clamp(AccessTokenMinutes, 1, 1_440));

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(Math.Clamp(RefreshTokenDays, 1, 90));

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();
        ApplyEnvironmentOverrides(options, configuration);

        return options;
    }

    public static void ApplyEnvironmentOverrides(JwtOptions options, IConfiguration configuration)
    {
        options.Issuer = configuration["JWT_ISSUER"] ?? options.Issuer;
        options.Audience = configuration["JWT_AUDIENCE"] ?? options.Audience;
        options.SigningKey = configuration["JWT_SIGNING_KEY"] ?? options.SigningKey;
        options.AccessTokenCookieName = configuration["JWT_ACCESS_TOKEN_COOKIE_NAME"] ?? options.AccessTokenCookieName;
        options.RefreshTokenCookieName = configuration["JWT_REFRESH_TOKEN_COOKIE_NAME"] ?? options.RefreshTokenCookieName;

        if (int.TryParse(configuration["JWT_ACCESS_TOKEN_MINUTES"], out var accessTokenMinutes))
        {
            options.AccessTokenMinutes = accessTokenMinutes;
        }

        if (int.TryParse(configuration["JWT_REFRESH_TOKEN_DAYS"], out var refreshTokenDays))
        {
            options.RefreshTokenDays = refreshTokenDays;
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            options.SigningKey = LocalDevelopmentSigningKey;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }
    }
}
