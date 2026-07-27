using CivicSignal.Api.Contracts.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CivicSignal.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class SystemController(
    EndpointDataSource endpoints,
    IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
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
                "ai-evaluation-quality-gates"
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
}
