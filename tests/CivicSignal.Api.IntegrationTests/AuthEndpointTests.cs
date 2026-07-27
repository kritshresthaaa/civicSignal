using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CivicSignal.Api.Contracts.Auth;
using CivicSignal.Application.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Api.IntegrationTests;

public sealed class AuthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid StaffUserId = Guid.Parse("99999999-9999-4999-9999-999999999999");
    private static readonly AuthUserDto StaffUser = new(
        StaffUserId,
        "admin@civicsignal.local",
        "Local Administrator",
        ["Administrator", "Operator", "Reviewer"]);

    [Fact]
    public async Task Csrf_endpoint_returns_request_token_and_cookie()
    {
        using var app = CreateFactory(new FakeAuthService());
        var client = CreateCookieTransparentClient(app);

        var csrf = await GetCsrfTokenAsync(client);

        Assert.Equal("X-CSRF-TOKEN", csrf.HeaderName);
        Assert.NotEmpty(csrf.Token);
        Assert.StartsWith("civicsignal_csrf=", csrf.Cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_returns_access_and_refresh_tokens_and_sets_http_only_cookies()
    {
        var loginResult = CreateTokenDto("access-token", "refresh-token");
        var auth = new FakeAuthService
        {
            LoginResult = loginResult
        };
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@civicsignal.local", password = "Admin123456!" })
        };

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        var setCookies = response.Headers.GetValues("Set-Cookie").ToArray();

        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal("access-token", body.AccessToken);
        Assert.Equal("refresh-token", body.RefreshToken);
        Assert.Equal("admin@civicsignal.local", auth.LoginInput?.Email);
        AssertSetCookieContains(setCookies, "civicsignal_access_token=access-token", "httponly", "path=/");
        AssertSetCookieContains(setCookies, "civicsignal_refresh_token=refresh-token", "httponly", "path=/api/auth");
    }

    [Fact]
    public async Task Refresh_uses_refresh_cookie_when_request_body_is_empty()
    {
        var refreshResult = CreateTokenDto("new-access-token", "new-refresh-token");
        var auth = new FakeAuthService
        {
            RefreshResult = refreshResult
        };
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "civicsignal_refresh_token=cookie-refresh-token");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        var setCookies = response.Headers.GetValues("Set-Cookie").ToArray();

        Assert.NotNull(body);
        Assert.Equal("new-access-token", body.AccessToken);
        Assert.Equal("new-refresh-token", body.RefreshToken);
        Assert.Equal("cookie-refresh-token", auth.RefreshedWith);
        AssertSetCookieContains(setCookies, "civicsignal_access_token=new-access-token", "httponly");
        AssertSetCookieContains(setCookies, "civicsignal_refresh_token=new-refresh-token", "httponly");
    }

    [Fact]
    public async Task Protected_cookie_authenticated_unsafe_request_without_csrf_is_rejected()
    {
        var auth = new FakeAuthService();
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/incidents/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/processing-status")
        {
            Content = JsonContent.Create(new { stepName = "MediaAnalysis", status = "Succeeded" })
        };
        request.Headers.Add("Cookie", $"civicsignal_access_token={CreateJwtAccessToken()}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(auth.LastPrincipal);
    }

    [Fact]
    public async Task Logout_revokes_refresh_cookie_and_deletes_auth_cookies()
    {
        var auth = new FakeAuthService();
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", "civicsignal_refresh_token=cookie-refresh-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToArray();

        Assert.Equal("cookie-refresh-token", auth.RevokedRefreshToken);
        AssertSetCookieContains(setCookies, "civicsignal_access_token=", "expires=");
        AssertSetCookieContains(setCookies, "civicsignal_refresh_token=", "expires=");
    }

    [Fact]
    public async Task Bearer_unsafe_request_does_not_require_csrf()
    {
        var auth = new FakeAuthService();
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = "body-refresh-token" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwtAccessToken());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("body-refresh-token", auth.RevokedRefreshToken);
    }

    [Fact]
    public async Task Bearer_access_token_authorizes_current_user_endpoint()
    {
        var auth = new FakeAuthService
        {
            CurrentUser = StaffUser
        };
        using var app = CreateFactory(auth);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtAccessToken());

        var response = await client.GetAsync("/api/auth/me");

        response.EnsureSuccessStatusCode();
        Assert.NotNull(auth.LastPrincipal);
        Assert.True(auth.LastPrincipal!.Identity?.IsAuthenticated);
        Assert.True(auth.LastPrincipal.IsInRole("Administrator"));
    }

    [Fact]
    public async Task Cookie_access_token_authorizes_current_user_endpoint()
    {
        var auth = new FakeAuthService
        {
            CurrentUser = StaffUser
        };
        using var app = CreateFactory(auth);
        var client = CreateCookieTransparentClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", $"civicsignal_access_token={CreateJwtAccessToken()}");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.NotNull(auth.LastPrincipal);
        Assert.True(auth.LastPrincipal!.Identity?.IsAuthenticated);
        Assert.True(auth.LastPrincipal.IsInRole("Administrator"));
    }

    private WebApplicationFactory<Program> CreateFactory(IAuthService auth)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton(auth);
            });
        });
    }

    private static HttpClient CreateCookieTransparentClient(WebApplicationFactory<Program> app)
    {
        return app.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    private static async Task<TestCsrfToken> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TestCsrfTokenResponse>();
        var cookie = ExtractCookie(response, "civicsignal_csrf");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.False(string.IsNullOrWhiteSpace(cookie));

        return new TestCsrfToken(body.HeaderName, body.Token, cookie);
    }

    private static void AddCsrf(HttpRequestMessage request, TestCsrfToken csrf, params string[] cookies)
    {
        request.Headers.Add(csrf.HeaderName, csrf.Token);
        request.Headers.Add("Cookie", string.Join("; ", new[] { csrf.Cookie }.Concat(cookies)));
    }

    private static string ExtractCookie(HttpResponseMessage response, string cookieName)
    {
        return response.Headers
            .GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])
            .Single(cookie => cookie.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase));
    }

    private static AuthTokenDto CreateTokenDto(string accessToken, string refreshToken)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var accessExpiresAt = utcNow.AddMinutes(15);
        var refreshExpiresAt = utcNow.AddDays(14);

        return new AuthTokenDto(
            "Bearer",
            accessToken,
            (long)(accessExpiresAt - utcNow).TotalSeconds,
            accessExpiresAt,
            refreshToken,
            (long)(refreshExpiresAt - utcNow).TotalSeconds,
            refreshExpiresAt);
    }

    private static string CreateJwtAccessToken()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(15);
        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "CivicSignal.Api",
            ["aud"] = "CivicSignal.Client",
            ["sub"] = StaffUserId.ToString(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            [ClaimTypes.NameIdentifier] = StaffUserId.ToString(),
            [ClaimTypes.Name] = StaffUser.Email,
            [ClaimTypes.Email] = StaffUser.Email,
            ["email"] = StaffUser.Email,
            ["role"] = StaffUser.Roles.ToArray()
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
            "local-development-only-civic-signal-jwt-signing-key-change-me-2026"));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken));

        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static void AssertSetCookieContains(IEnumerable<string> cookies, params string[] fragments)
    {
        Assert.Contains(cookies, cookie =>
            fragments.All(fragment => cookie.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeAuthService : IAuthService
    {
        public LoginInput? LoginInput { get; private set; }

        public AuthTokenDto? LoginResult { get; init; }

        public string? RefreshedWith { get; private set; }

        public AuthTokenDto? RefreshResult { get; init; }

        public string? RevokedRefreshToken { get; private set; }

        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public AuthUserDto? CurrentUser { get; init; }

        public Task<AuthUserDto> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthTokenDto?> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
        {
            LoginInput = input;
            return Task.FromResult(LoginResult);
        }

        public Task<AuthTokenDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshedWith = refreshToken;
            return Task.FromResult(RefreshResult);
        }

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RevokedRefreshToken = refreshToken;
            return Task.CompletedTask;
        }

        public Task<AuthUserDto?> ValidateCredentialsAsync(LoginInput input, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        {
            LastPrincipal = principal;
            return Task.FromResult(CurrentUser);
        }
    }

    private sealed record TestCsrfTokenResponse(string HeaderName, string Token);

    private sealed record TestCsrfToken(string HeaderName, string Token, string Cookie);
}
