using CivicSignal.Api.Contracts.System;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CivicSignal.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class SystemController(
    EndpointDataSource endpoints,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    CivicSignalDbContext dbContext) : ControllerBase
{
    [HttpGet("capabilities")]
    [ProducesResponseType<SystemCapabilitiesResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemCapabilitiesResponse> GetCapabilities()
    {
        var routes = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .Where(route => !string.IsNullOrWhiteSpace(route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new SystemCapabilitiesResponse(
            Service: "CivicSignal.Api",
            Environment: environment.EnvironmentName,
            GeneratedAt: DateTimeOffset.UtcNow,
            Features:
            [
                "incident-intake",
                "incident-review",
                "identity-rbac",
                "public-tracking",
                "public-incident-feed",
                "historical-complaints",
                "data-import-jobs",
                "forecasting",
                "signalr-realtime",
                "weather-context",
                "osm-nominatim-geocoding",
                "controlled-agent-workflow",
                "model-lab-classifier",
                "ai-evaluation-baselines",
                "ai-evaluation-quality-gates",
                "operational-health-checks"
            ],
            Routes: routes));
    }

    [HttpGet("integrations")]
    [ProducesResponseType<SystemIntegrationStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemIntegrationStatusResponse> GetIntegrations()
    {
        var fileStorageProvider = configuration["FileStorage:Provider"] ?? "Local";
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        var rabbitEnabled = configuration.GetValue<bool>("RabbitMq:Enabled");
        var aiServiceEnabled = configuration.GetValue<bool>("AiService:Enabled");
        var aiRemoteEmbeddings = configuration.GetValue("AiService:UseRemoteEmbeddings", true);
        var openAiEnabled = configuration.GetValue<bool>("OpenAI:Enabled");
        var weatherEnabled = configuration.GetValue<bool>("Weather:Enabled");
        var nominatimEnabled = configuration.GetValue<bool>("Nominatim:Enabled");
        var demoDataEnabled = configuration.GetValue<bool>("DemoData:Enabled");

        SystemIntegrationStatusDto[] integrations =
        [
            new("PostgreSQL/PostGIS", "Persistence", "Configured", true, "EF Core uses PostgreSQL with NetTopologySuite geospatial mappings."),
            new("pgvector", "Persistence", "Configured", true, "Incident text embeddings and duplicate search use the pgvector extension."),
            new("Identity/JWT", "Security", "Configured", true, "Staff access uses ASP.NET Core Identity, JWT access tokens, and refresh-token cookies."),
            new("SignalR", "Realtime", "Configured", true, "Incident status events are published through the operations hub."),
            new(
                "File storage",
                "Storage",
                string.Equals(fileStorageProvider, "Local", StringComparison.OrdinalIgnoreCase) ? "Local" : "S3-compatible",
                true,
                string.Equals(fileStorageProvider, "Local", StringComparison.OrdinalIgnoreCase)
                    ? "Incident media is stored on local disk for development."
                    : "Incident media is stored through the configured S3-compatible adapter."),
            new(
                "Redis cache",
                "Cache",
                redisEnabled ? "Enabled" : "Disabled",
                redisEnabled,
                redisEnabled ? "Forecast and read-model caching uses Redis." : "Redis is disabled; cache calls use direct backend reads."),
            new(
                "RabbitMQ worker queue",
                "Messaging",
                rabbitEnabled ? "Enabled" : "Database polling fallback",
                rabbitEnabled,
                rabbitEnabled ? "Incident and data-import jobs are queued through RabbitMQ." : "RabbitMQ is disabled; the worker can use local polling fallback."),
            new(
                "Python AI service",
                "AI",
                aiServiceEnabled ? "Enabled" : "Heuristic fallback",
                aiServiceEnabled,
                aiServiceEnabled
                    ? $"AI service adapter is enabled; remote embeddings are {(aiRemoteEmbeddings ? "enabled" : "disabled")}."
                    : "AI service is disabled; deterministic local analyzers handle triage, media, and embeddings."),
            new(
                "OpenAI analyzer",
                "AI",
                openAiEnabled ? "Enabled" : "Disabled",
                openAiEnabled,
                openAiEnabled ? "OpenAI analyzer is enabled when an API key is configured." : "OpenAI analyzer is disabled in this environment."),
            new(
                "Weather API",
                "External API",
                weatherEnabled ? "Enabled" : "Disabled",
                weatherEnabled,
                weatherEnabled ? "Controlled workflows can request weather context." : "Weather lookup returns unavailable tool results."),
            new(
                "Nominatim geocoding",
                "External API",
                nominatimEnabled ? "Enabled" : "Disabled",
                nominatimEnabled,
                nominatimEnabled ? "Address search and reverse geocoding use Nominatim through the backend." : "Geocoding falls back to deterministic local behavior."),
            new("NYC 311 import", "Open Data", "Configured", true, "Historical complaint import uses the NYC Open Data API through backend jobs."),
            new("Forecasting", "Analytics", "Baseline", true, "The API exposes a moving-average/trend forecasting baseline."),
            new(
                "Demo data seeding",
                "Development",
                demoDataEnabled ? "Enabled" : "Disabled",
                demoDataEnabled,
                demoDataEnabled ? "Startup can seed portfolio demo incidents." : "Startup demo seeding is disabled.")
        ];

        return Ok(new SystemIntegrationStatusResponse(
            Service: "CivicSignal.Api",
            Environment: environment.EnvironmentName,
            GeneratedAt: DateTimeOffset.UtcNow,
            Integrations: integrations));
    }

    [HttpGet("runtime-policy")]
    [ProducesResponseType<SystemRuntimePolicyResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemRuntimePolicyResponse> GetRuntimePolicy()
    {
        return Ok(new SystemRuntimePolicyResponse(
            DuplicateMinimumScore: configuration.GetValue("DuplicateDetection:MinimumScore", 0.7),
            DuplicateSearchRadiusMeters: configuration.GetValue("DuplicateDetection:SearchRadiusMeters", 500.0),
            DuplicateTimeWindowHours: configuration.GetValue("DuplicateDetection:TimeWindowHours", 168),
            DuplicateCandidatePoolSize: configuration.GetValue("DuplicateDetection:CandidatePoolSize", 50),
            DuplicateMaxResults: configuration.GetValue("DuplicateDetection:MaxResults", 5),
            TextEmbeddingDimensions: configuration.GetValue("TextEmbeddings:Dimensions", 1024),
            MaxUploadBytes: configuration.GetValue("FileStorage:MaxUploadBytes", 10_485_760L),
            AiServiceEnabled: configuration.GetValue<bool>("AiService:Enabled"),
            RemoteEmbeddingsEnabled: configuration.GetValue("AiService:UseRemoteEmbeddings", true),
            RedisEnabled: configuration.GetValue<bool>("Redis:Enabled"),
            RabbitMqEnabled: configuration.GetValue<bool>("RabbitMq:Enabled"),
            WeatherEnabled: configuration.GetValue<bool>("Weather:Enabled"),
            GeocodingEnabled: configuration.GetValue<bool>("Nominatim:Enabled")));
    }

    [HttpGet("health")]
    [ProducesResponseType<SystemHealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemHealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        List<SystemHealthCheckDto> checks =
        [
            new(
                Name: "API process",
                Category: "Runtime",
                Status: "Healthy",
                Critical: true,
                Detail: "ASP.NET Core request pipeline is responding."),
            new(
                Name: "Request correlation",
                Category: "Observability",
                Status: "Configured",
                Critical: false,
                Detail: "Responses include X-Correlation-ID for log and request tracing.")
        ];

        await AddDatabaseChecksAsync(checks, cancellationToken);
        AddStorageCheck(checks);
        AddConfiguredIntegrationChecks(checks);

        var status = ResolveOverallStatus(checks);

        return Ok(new SystemHealthResponse(
            Service: "CivicSignal.Api",
            Environment: environment.EnvironmentName,
            Status: status,
            GeneratedAt: DateTimeOffset.UtcNow,
            Checks: checks));
    }

    private async Task<bool> AddDatabaseChecksAsync(
        List<SystemHealthCheckDto> checks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            checks.Add(new SystemHealthCheckDto(
                Name: "PostgreSQL connection",
                Category: "Persistence",
                Status: canConnect ? "Healthy" : "Unavailable",
                Critical: true,
                Detail: canConnect
                    ? "EF Core can connect to the CivicSignal database."
                    : "The API cannot connect to PostgreSQL.",
                LatencyMilliseconds: stopwatch.ElapsedMilliseconds));

            if (!canConnect)
            {
                checks.Add(new SystemHealthCheckDto(
                    Name: "PostGIS and pgvector extensions",
                    Category: "Persistence",
                    Status: "Skipped",
                    Critical: true,
                    Detail: "Extension validation was skipped because PostgreSQL is unavailable."));

                return false;
            }

            await AddDatabaseExtensionCheckAsync(checks, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            checks.Add(new SystemHealthCheckDto(
                Name: "PostgreSQL connection",
                Category: "Persistence",
                Status: "Unavailable",
                Critical: true,
                Detail: $"Database check failed: {exception.GetType().Name}.",
                LatencyMilliseconds: stopwatch.ElapsedMilliseconds));

            checks.Add(new SystemHealthCheckDto(
                Name: "PostGIS and pgvector extensions",
                Category: "Persistence",
                Status: "Skipped",
                Critical: true,
                Detail: "Extension validation was skipped because PostgreSQL is unavailable."));

            return false;
        }
    }

    private async Task AddDatabaseExtensionCheckAsync(
        List<SystemHealthCheckDto> checks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var extensions = await dbContext.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT extname AS "Value"
                    FROM pg_extension
                    WHERE extname IN ('postgis', 'vector')
                    """)
                .ToListAsync(cancellationToken);
            stopwatch.Stop();

            var hasPostgis = extensions.Contains("postgis", StringComparer.OrdinalIgnoreCase);
            var hasVector = extensions.Contains("vector", StringComparer.OrdinalIgnoreCase);
            var status = hasPostgis && hasVector ? "Healthy" : "Degraded";

            checks.Add(new SystemHealthCheckDto(
                Name: "PostGIS and pgvector extensions",
                Category: "Persistence",
                Status: status,
                Critical: true,
                Detail: status == "Healthy"
                    ? "PostGIS and pgvector extensions are installed."
                    : $"Installed extensions: {string.Join(", ", extensions.DefaultIfEmpty("none"))}.",
                LatencyMilliseconds: stopwatch.ElapsedMilliseconds));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            checks.Add(new SystemHealthCheckDto(
                Name: "PostGIS and pgvector extensions",
                Category: "Persistence",
                Status: "Degraded",
                Critical: true,
                Detail: $"Extension validation failed: {exception.GetType().Name}.",
                LatencyMilliseconds: stopwatch.ElapsedMilliseconds));
        }
    }

    private void AddStorageCheck(List<SystemHealthCheckDto> checks)
    {
        var provider = configuration["FileStorage:Provider"] ?? "Local";
        var rootPath = ResolveConfiguredPath(
            environment.ContentRootPath,
            configuration["FileStorage:RootPath"],
            "../../var/uploads/incident-media");

        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "MinIO", StringComparison.OrdinalIgnoreCase))
        {
            var bucket = configuration["S3Storage:BucketName"];
            var endpoint = configuration["S3Storage:Endpoint"];

            checks.Add(new SystemHealthCheckDto(
                Name: "Object storage",
                Category: "Storage",
                Status: string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(endpoint)
                    ? "Degraded"
                    : "Configured",
                Critical: true,
                Detail: string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(endpoint)
                    ? "S3-compatible storage is selected but endpoint or bucket configuration is missing."
                    : $"S3-compatible storage is configured for bucket '{bucket}'."));

            return;
        }

        checks.Add(new SystemHealthCheckDto(
            Name: "Object storage",
            Category: "Storage",
            Status: Directory.Exists(rootPath) ? "Healthy" : "Degraded",
            Critical: true,
            Detail: Directory.Exists(rootPath)
                ? "Local incident media storage path exists."
                : "Local incident media storage path does not exist yet."));
    }

    private void AddConfiguredIntegrationChecks(List<SystemHealthCheckDto> checks)
    {
        AddConfiguredIntegrationCheck(
            checks,
            name: "Redis cache",
            category: "Cache",
            enabled: configuration.GetValue<bool>("Redis:Enabled"),
            configuredDetail: "Redis is enabled for cached read models and forecasting responses.",
            disabledDetail: "Redis is disabled; the API uses direct backend reads.");
        AddConfiguredIntegrationCheck(
            checks,
            name: "RabbitMQ queues",
            category: "Messaging",
            enabled: configuration.GetValue<bool>("RabbitMq:Enabled"),
            configuredDetail: "RabbitMQ is enabled for incident processing and data import jobs.",
            disabledDetail: "RabbitMQ is disabled; background workers can use configured fallbacks.");
        AddConfiguredIntegrationCheck(
            checks,
            name: "Python AI service",
            category: "AI",
            enabled: configuration.GetValue<bool>("AiService:Enabled"),
            configuredDetail: $"AI service is configured at {configuration["AiService:BaseUrl"] ?? "the configured base URL"}.",
            disabledDetail: "AI service is disabled; deterministic fallback analyzers are active.");
        AddConfiguredIntegrationCheck(
            checks,
            name: "Weather API",
            category: "External API",
            enabled: configuration.GetValue<bool>("Weather:Enabled"),
            configuredDetail: "Weather context is enabled for controlled triage workflows.",
            disabledDetail: "Weather context is disabled by configuration.");
        AddConfiguredIntegrationCheck(
            checks,
            name: "Nominatim geocoding",
            category: "External API",
            enabled: configuration.GetValue<bool>("Nominatim:Enabled"),
            configuredDetail: "Geocoding is enabled for address search and reverse lookup.",
            disabledDetail: "Geocoding is disabled by configuration.");
    }

    private static void AddConfiguredIntegrationCheck(
        List<SystemHealthCheckDto> checks,
        string name,
        string category,
        bool enabled,
        string configuredDetail,
        string disabledDetail)
    {
        checks.Add(new SystemHealthCheckDto(
            Name: name,
            Category: category,
            Status: enabled ? "Configured" : "Disabled",
            Critical: false,
            Detail: enabled ? configuredDetail : disabledDetail));
    }

    private static string ResolveOverallStatus(IReadOnlyCollection<SystemHealthCheckDto> checks)
    {
        if (checks.Any(check => check.Critical && check.Status is "Unavailable"))
        {
            return "Unhealthy";
        }

        if (checks.Any(check => check.Status is "Degraded" or "Skipped"))
        {
            return "Degraded";
        }

        return "Healthy";
    }

    private static string ResolveConfiguredPath(
        string contentRootPath,
        string? configuredPath,
        string defaultPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(contentRootPath, path));
    }
}
