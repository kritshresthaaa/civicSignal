namespace CivicSignal.Api.Contracts.System;

public sealed record SystemCapabilitiesResponse(
    string Service,
    string Environment,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<string> Features,
    IReadOnlyCollection<string> Routes);

public sealed record SystemIntegrationStatusResponse(
    string Service,
    string Environment,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<SystemIntegrationStatusDto> Integrations);

public sealed record SystemIntegrationStatusDto(
    string Name,
    string Category,
    string Status,
    bool Enabled,
    string Detail);

public sealed record SystemRuntimePolicyResponse(
    double DuplicateMinimumScore,
    double DuplicateSearchRadiusMeters,
    int DuplicateTimeWindowHours,
    int DuplicateCandidatePoolSize,
    int DuplicateMaxResults,
    int TextEmbeddingDimensions,
    long MaxUploadBytes,
    bool AiServiceEnabled,
    bool RemoteEmbeddingsEnabled,
    bool RedisEnabled,
    bool RabbitMqEnabled,
    bool WeatherEnabled,
    bool GeocodingEnabled);
