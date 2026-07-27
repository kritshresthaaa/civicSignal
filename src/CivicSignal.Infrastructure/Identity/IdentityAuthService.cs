using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CivicSignal.Application.Common;
using CivicSignal.Application.Identity;
using CivicSignal.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Identity;

internal sealed class IdentityAuthService(
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles,
    CivicSignalDbContext dbContext,
    IClock clock,
    IOptions<JwtOptions> jwtOptions,
    IValidator<RegisterUserInput> registerValidator,
    IValidator<LoginInput> loginValidator) : IAuthService
{
    public async Task<AuthUserDto> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
    {
        await registerValidator.ValidateAndThrowAsync(input, cancellationToken);

        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim(),
            CreatedAt = clock.UtcNow
        };

        var createResult = await users.CreateAsync(user, input.Password);
        ThrowIfFailed(createResult);

        await EnsureRoleExistsAsync(CivicSignalRoles.Reporter);
        var roleResult = await users.AddToRoleAsync(user, CivicSignalRoles.Reporter);
        ThrowIfFailed(roleResult);

        return await ToDtoAsync(user);
    }

    public async Task<AuthTokenDto?> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
    {
        var user = await ValidateUserAsync(input, cancellationToken);

        return user is null ? null : await IssueTokenPairAsync(user, cancellationToken);
    }

    public async Task<AuthTokenDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var utcNow = clock.UtcNow;
        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken?.User is null)
        {
            return null;
        }

        if (!storedToken.IsActive(utcNow))
        {
            storedToken.Revoke(utcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var replacementRefreshToken = GenerateRefreshToken();
        var replacementRefreshTokenHash = HashRefreshToken(replacementRefreshToken);
        var refreshTokenExpiresAt = utcNow.Add(jwtOptions.Value.RefreshTokenLifetime);

        storedToken.Revoke(utcNow, replacementRefreshTokenHash);
        dbContext.RefreshTokens.Add(ApplicationRefreshToken.Create(
            storedToken.UserId,
            replacementRefreshTokenHash,
            utcNow,
            refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CreateTokenDtoAsync(storedToken.User, replacementRefreshToken, refreshTokenExpiresAt);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        storedToken.Revoke(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserDto?> ValidateCredentialsAsync(LoginInput input, CancellationToken cancellationToken = default)
    {
        var user = await ValidateUserAsync(input, cancellationToken);
        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var user = await users.FindByIdAsync(userId);
        return user is null ? null : await ToDtoAsync(user);
    }

    private async Task<ApplicationUser?> ValidateUserAsync(LoginInput input, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(input, cancellationToken);

        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null)
        {
            return null;
        }

        var passwordValid = await users.CheckPasswordAsync(user, input.Password);
        return passwordValid ? user : null;
    }

    private async Task<AuthTokenDto> IssueTokenPairAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var refreshToken = GenerateRefreshToken();
        var refreshTokenHash = HashRefreshToken(refreshToken);
        var utcNow = clock.UtcNow;
        var refreshTokenExpiresAt = utcNow.Add(jwtOptions.Value.RefreshTokenLifetime);

        dbContext.RefreshTokens.Add(ApplicationRefreshToken.Create(
            user.Id,
            refreshTokenHash,
            utcNow,
            refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CreateTokenDtoAsync(user, refreshToken, refreshTokenExpiresAt);
    }

    private async Task<AuthTokenDto> CreateTokenDtoAsync(
        ApplicationUser user,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        var options = jwtOptions.Value;
        var utcNow = clock.UtcNow;
        var accessTokenExpiresAt = utcNow.Add(options.AccessTokenLifetime);
        var roles = await users.GetRolesAsync(user);
        var accessToken = CreateJwtAccessToken(user, roles, utcNow, accessTokenExpiresAt, options);

        return new AuthTokenDto(
            "Bearer",
            accessToken,
            Math.Max(0, (long)(accessTokenExpiresAt - utcNow).TotalSeconds),
            accessTokenExpiresAt,
            refreshToken,
            Math.Max(0, (long)(refreshTokenExpiresAt - utcNow).TotalSeconds),
            refreshTokenExpiresAt);
    }

    private static string CreateJwtAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        JwtOptions options)
    {
        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["sub"] = user.Id.ToString(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            [ClaimTypes.NameIdentifier] = user.Id.ToString(),
            [ClaimTypes.Name] = user.Email ?? string.Empty,
            [ClaimTypes.Email] = user.Email ?? string.Empty,
            ["email"] = user.Email ?? string.Empty,
            ["role"] = roles.ToArray()
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            payload["display_name"] = user.DisplayName;
        }

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken));

        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static string GenerateRefreshToken()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (await roles.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roles.CreateAsync(new ApplicationRole
        {
            Name = roleName
        });

        ThrowIfFailed(result);
    }

    private async Task<AuthUserDto> ToDtoAsync(ApplicationUser user)
    {
        var roleNames = await users.GetRolesAsync(user);

        return new AuthUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roleNames.ToArray());
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var failures = result.Errors
            .Select(error => new ValidationFailure(error.Code, error.Description))
            .ToArray();

        throw new ValidationException(failures);
    }
}
