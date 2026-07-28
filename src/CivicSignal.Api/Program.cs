using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using CivicSignal.Api.Hubs;
using CivicSignal.Api.Security;
using CivicSignal.Application.Abstractions.Realtime;
using CivicSignal.Application.Common;
using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.Identity;
using CivicSignal.Infrastructure.DemoData;
using FluentValidation;
using CivicSignal.Infrastructure.DependencyInjection;
using CivicSignal.Infrastructure.Identity;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var frontendOrigins = GetFrontendOrigins(builder.Configuration);
const string frontendCorsPolicy = "Frontend";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
var dataProtectionKeysPath = ResolveDataProtectionKeysPath(
    builder.Environment.ContentRootPath,
    builder.Configuration["DataProtection:KeysPath"]);
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("CivicSignal.Api");
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "civicsignal_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        ConfigureFrontendCorsPolicy(policy, frontendOrigins, builder.Environment.IsDevelopment());
    });

    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        ConfigureFrontendCorsPolicy(policy, frontendOrigins, builder.Environment.IsDevelopment());
    });
});
var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrWhiteSpace(context.Token))
                {
                    return Task.CompletedTask;
                }

                if (context.Request.Cookies.TryGetValue(jwtOptions.AccessTokenCookieName, out var accessToken))
                {
                    context.Token = accessToken;
                    return Task.CompletedTask;
                }

                if (context.Request.Path.StartsWithSegments("/hubs/incidents")
                    && context.Request.Query.TryGetValue("access_token", out var hubAccessToken))
                {
                    context.Token = hubAccessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Rate limit exceeded. Please wait before trying again."
        }, cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(context),
            _ => CreateFixedWindowOptions(600, TimeSpan.FromMinutes(1))));

    options.AddPolicy(SecurityRateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(context),
            _ => CreateFixedWindowOptions(20, TimeSpan.FromMinutes(1))));

    options.AddPolicy(SecurityRateLimitPolicies.PublicWrite, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(context),
            _ => CreateFixedWindowOptions(60, TimeSpan.FromMinutes(1))));
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CivicSignalPolicies.IncidentReview, policy =>
        policy.RequireRole(
            CivicSignalRoles.Administrator,
            CivicSignalRoles.Operator,
            CivicSignalRoles.Reviewer));

    options.AddPolicy(CivicSignalPolicies.IncidentOperations, policy =>
        policy.RequireRole(
            CivicSignalRoles.Administrator,
            CivicSignalRoles.Operator));
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddScoped<IIncidentRealtimeNotifier, SignalRIncidentRealtimeNotifier>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CivicSignal API",
        Version = "v1",
        Description = "Backend API for CivicSignal AI incident triage and operations workflows."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the access token returned from POST /api/auth/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
});

var app = builder.Build();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var requestCorrelationLogger = loggerFactory.CreateLogger("CivicSignal.RequestCorrelation");

var mediaRootPath = ResolveMediaRootPath(
    app.Environment.ContentRootPath,
    app.Configuration["FileStorage:RootPath"]);
Directory.CreateDirectory(mediaRootPath);

await app.Services.ApplyDatabaseMigrationsAsync(app.Configuration, app.Logger);
await app.Services.SeedDevelopmentIdentityAsync(app.Configuration, app.Logger);
await app.Services.SeedDemoDataAsync(app.Configuration, app.Logger);

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError ? "Unexpected error" : "Request failed",
            Detail = exception?.Message
        });
    });
});

UseRequestCorrelation(app, requestCorrelationLogger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

UseSecurityHeaders(app);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CivicSignal API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRootPath),
    RequestPath = app.Configuration["FileStorage:PublicBasePath"] ?? "/media"
});

app.UseAuthentication();
app.UseRateLimiter();
UseCsrfProtection(app, jwtOptions);
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "CivicSignal.Api"
}))
.AllowAnonymous()
.WithTags("Health")
.WithName("Health");

app.MapGet("/health/ready", async (CivicSignalDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "Ready", database = "Connected" })
        : Results.Problem(
            title: "Service is not ready",
            detail: "The API could not connect to PostgreSQL.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous()
.WithTags("Health")
.WithName("Readiness");

app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);

    return Results.Ok(new
    {
        headerName = tokens.HeaderName ?? "X-CSRF-TOKEN",
        token = tokens.RequestToken
    });
})
.AllowAnonymous()
.RequireCors(frontendCorsPolicy)
.RequireRateLimiting(SecurityRateLimitPolicies.Auth)
.WithTags("Auth")
.WithName("GetCsrfToken");

app.MapControllers().RequireCors(frontendCorsPolicy);
app.MapHub<IncidentStatusHub>("/hubs/incidents").RequireCors(frontendCorsPolicy);

app.Run();

static string ResolveMediaRootPath(string contentRootPath, string? configuredPath)
{
    var rootPath = string.IsNullOrWhiteSpace(configuredPath)
        ? "../../var/uploads/incident-media"
        : configuredPath.Trim();

    return Path.IsPathRooted(rootPath)
        ? Path.GetFullPath(rootPath)
        : Path.GetFullPath(Path.Combine(contentRootPath, rootPath));
}

static string ResolveDataProtectionKeysPath(string contentRootPath, string? configuredPath)
{
    var keysPath = string.IsNullOrWhiteSpace(configuredPath)
        ? "../../var/data-protection-keys"
        : configuredPath.Trim();

    return Path.IsPathRooted(keysPath)
        ? Path.GetFullPath(keysPath)
        : Path.GetFullPath(Path.Combine(contentRootPath, keysPath));
}

static string[] GetFrontendOrigins(IConfiguration configuration)
{
    string[] localDevelopmentOrigins =
    [
        "http://localhost:3000",
        "http://localhost:3001",
        "http://127.0.0.1:3000",
        "http://127.0.0.1:3001"
    ];
    var configuredOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    var frontendUrl = configuration["FRONTEND_URL"];
    var origins = configuredOrigins.Concat(localDevelopmentOrigins);

    if (!string.IsNullOrWhiteSpace(frontendUrl))
    {
        origins = origins.Append(frontendUrl.Trim());
    }

    return origins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static void ConfigureFrontendCorsPolicy(CorsPolicyBuilder policy, string[] frontendOrigins, bool allowDevelopmentLanOrigins)
{
    if (allowDevelopmentLanOrigins)
    {
        policy.SetIsOriginAllowed(origin =>
            frontendOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            || IsDevelopmentLanFrontendOrigin(origin));
    }
    else
    {
        policy.WithOrigins(frontendOrigins);
    }

    policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}

static bool IsDevelopmentLanFrontendOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        || uri.Scheme is not ("http" or "https")
        || uri.Port is not (3000 or 3001))
    {
        return false;
    }

    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!IPAddress.TryParse(uri.Host, out var address))
    {
        return false;
    }

    if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal)
    {
        return true;
    }

    var bytes = address.GetAddressBytes();
    return address.AddressFamily switch
    {
        System.Net.Sockets.AddressFamily.InterNetwork => bytes[0] == 10
            || bytes[0] == 192 && bytes[1] == 168
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
            || bytes[0] == 169 && bytes[1] == 254,
        System.Net.Sockets.AddressFamily.InterNetworkV6 => (bytes[0] & 0xfe) == 0xfc,
        _ => false
    };
}

static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit, TimeSpan window)
{
    return new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = permitLimit,
        QueueLimit = 0,
        Window = window
    };
}

static string GetClientPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return $"ip:{forwardedFor.Split(',')[0].Trim()}";
    }

    return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

static bool IsUnsafeMethod(string method)
{
    return !HttpMethods.IsGet(method)
        && !HttpMethods.IsHead(method)
        && !HttpMethods.IsOptions(method)
        && !HttpMethods.IsTrace(method);
}

static bool HasBearerAuthorization(HttpRequest request)
{
    var authorization = request.Headers.Authorization.FirstOrDefault();
    return authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true;
}

static bool HasAuthCookie(HttpRequest request, JwtOptions jwtOptions)
{
    return request.Cookies.ContainsKey(jwtOptions.AccessTokenCookieName)
        || request.Cookies.ContainsKey(jwtOptions.RefreshTokenCookieName);
}

static bool ShouldValidateCsrf(HttpRequest request, JwtOptions jwtOptions)
{
    if (!IsUnsafeMethod(request.Method)
        || !request.Path.StartsWithSegments("/api")
        || IsCsrfExemptEndpoint(request)
        || HasBearerAuthorization(request))
    {
        return false;
    }

    return HasAuthCookie(request, jwtOptions);
}

static bool IsCsrfExemptEndpoint(HttpRequest request)
{
    var path = request.Path;

    return path.StartsWithSegments("/api/auth/csrf")
        || path.StartsWithSegments("/api/auth/register")
        || path.StartsWithSegments("/api/auth/login")
        || path.StartsWithSegments("/api/auth/refresh")
        || path.StartsWithSegments("/api/auth/logout")
        || path.StartsWithSegments("/api/model-lab")
        || path.StartsWithSegments("/api/public")
        || (HttpMethods.IsPost(request.Method)
            && string.Equals(path.Value, "/api/incidents", StringComparison.OrdinalIgnoreCase));
}

static IApplicationBuilder UseCsrfProtection(IApplicationBuilder app, JwtOptions jwtOptions)
{
    return app.Use(async (context, next) =>
    {
        if (!ShouldValidateCsrf(context.Request, jwtOptions))
        {
            await next();
            return;
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid CSRF token",
                Detail = "Cookie-authenticated unsafe requests must include a valid CSRF token."
            });
            return;
        }

        await next();
    });
}

static IApplicationBuilder UseSecurityHeaders(IApplicationBuilder app)
{
    return app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
        headers.TryAdd("X-Download-Options", "noopen");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        await next();
    });
}

static IApplicationBuilder UseRequestCorrelation(IApplicationBuilder app, ILogger logger)
{
    return app.Use(async (context, next) =>
    {
        const string correlationHeader = "X-Correlation-ID";
        var correlationId = context.Request.Headers[correlationHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = context.TraceIdentifier;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[correlationHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next();
        }
    });
}

public partial class Program
{
}
