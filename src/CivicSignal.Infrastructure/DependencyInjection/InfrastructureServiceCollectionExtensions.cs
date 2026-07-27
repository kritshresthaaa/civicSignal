using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Application.Abstractions.Geocoding;
using CivicSignal.Application.Abstractions.OpenData;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Common;
using CivicSignal.Application.Identity;
using CivicSignal.Infrastructure.Ai;
using CivicSignal.Infrastructure.Caching;
using CivicSignal.Infrastructure.Duplicates;
using CivicSignal.Infrastructure.Geocoding;
using CivicSignal.Infrastructure.Identity;
using CivicSignal.Infrastructure.Messaging;
using CivicSignal.Infrastructure.OpenData;
using CivicSignal.Infrastructure.Persistence;
using CivicSignal.Infrastructure.Persistence.Repositories;
using CivicSignal.Infrastructure.Storage;
using CivicSignal.Infrastructure.Weather;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CivicSignal")
            ?? configuration["POSTGRES_CONNECTION_STRING"]
            ?? "Host=localhost;Port=5432;Database=civic_signal;Username=postgres;Password=postgres";

        services.AddDbContext<CivicSignalDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.UseVector();
                }));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<CivicSignalDbContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CivicSignalDbContext>());
        services.AddScoped(typeof(IGenericRepository<>), typeof(EfGenericRepository<>));
        services.AddScoped<IIncidentRepository, EfIncidentRepository>();
        services.AddScoped<IDataImportJobRepository, EfDataImportJobRepository>();
        services.AddScoped<IHistoricalComplaintRepository, EfHistoricalComplaintRepository>();
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<AiServiceOptions>(configuration.GetSection(AiServiceOptions.SectionName));
        services.PostConfigure<OpenAiOptions>(options =>
        {
            options.ApiKey ??= configuration["OPENAI_API_KEY"];
        });
        services.Configure<TextEmbeddingOptions>(configuration.GetSection(TextEmbeddingOptions.SectionName));
        services.Configure<DuplicateDetectionOptions>(configuration.GetSection(DuplicateDetectionOptions.SectionName));
        services.Configure<LocalFileStorageOptions>(configuration.GetSection(LocalFileStorageOptions.SectionName));
        services.Configure<S3FileStorageOptions>(configuration.GetSection(S3FileStorageOptions.SectionName));
        services.Configure<CivicSignalRedisOptions>(configuration.GetSection(CivicSignalRedisOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<Nyc311Options>(configuration.GetSection(Nyc311Options.SectionName));
        services.Configure<NominatimOptions>(configuration.GetSection(NominatimOptions.SectionName));
        services.Configure<WeatherOptions>(configuration.GetSection(WeatherOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.PostConfigure<JwtOptions>(options =>
            JwtOptions.ApplyEnvironmentOverrides(options, configuration));
        services.PostConfigure<Nyc311Options>(options =>
        {
            options.BaseUrl = configuration["NYC311_BASE_URL"] ?? options.BaseUrl;
            options.ResourcePath = configuration["NYC311_RESOURCE_PATH"] ?? options.ResourcePath;
            options.AppToken ??= configuration["NYC311_APP_TOKEN"];

            if (int.TryParse(configuration["NYC311_TIMEOUT_SECONDS"], out var timeoutSeconds))
            {
                options.TimeoutSeconds = timeoutSeconds;
            }

            if (int.TryParse(configuration["NYC311_DEFAULT_LIMIT"], out var defaultLimit))
            {
                options.DefaultLimit = defaultLimit;
            }

            if (int.TryParse(configuration["NYC311_MAX_LIMIT"], out var maxLimit))
            {
                options.MaxLimit = maxLimit;
            }
        });
        services.AddHttpClient<INyc311ComplaintClient, Nyc311ComplaintClient>(ConfigureNyc311HttpClient);
        RegisterGeocoding(services, configuration);
        RegisterWeather(services, configuration);
        RegisterCaching(services, configuration);
        RegisterMessaging(services, configuration);

        services.AddScoped<HeuristicIncidentAnalyzer>();
        services.AddScoped<HeuristicIncidentMediaAnalyzer>();
        services.AddScoped<HashingTextEmbeddingGenerator>();

        var aiServiceSection = configuration.GetSection(AiServiceOptions.SectionName);
        var aiServiceEnabled = aiServiceSection.GetValue<bool>(nameof(AiServiceOptions.Enabled));
        var useRemoteEmbeddings = aiServiceSection.GetValue(
            nameof(AiServiceOptions.UseRemoteEmbeddings),
            defaultValue: true);

        if (aiServiceEnabled)
        {
            services
                .AddHttpClient<AiServiceIncidentAnalyzer>(ConfigureAiServiceHttpClient);
            services
                .AddHttpClient<AiServiceIncidentMediaAnalyzer>(ConfigureAiServiceHttpClient);
            services.AddScoped<IAiIncidentAnalyzer, ResilientAiIncidentAnalyzer>();
            services.AddScoped<IIncidentMediaAnalyzer, ResilientIncidentMediaAnalyzer>();

            if (useRemoteEmbeddings)
            {
                services
                    .AddHttpClient<AiServiceTextEmbeddingGenerator>(ConfigureAiServiceHttpClient);
                services.AddScoped<ITextEmbeddingGenerator, ResilientTextEmbeddingGenerator>();
            }
            else
            {
                services.AddScoped<ITextEmbeddingGenerator>(provider =>
                    provider.GetRequiredService<HashingTextEmbeddingGenerator>());
            }
        }
        else
        {
            RegisterConfiguredIncidentAnalyzer(services, configuration);
            services.AddScoped<IIncidentMediaAnalyzer>(provider =>
                provider.GetRequiredService<HeuristicIncidentMediaAnalyzer>());
            services.AddScoped<ITextEmbeddingGenerator>(provider =>
                provider.GetRequiredService<HashingTextEmbeddingGenerator>());
        }

        services.AddScoped<IDuplicateIncidentSearchService, PgvectorDuplicateIncidentSearchService>();
        RegisterFileStorage(services, configuration);
        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddSingleton<IClock, SystemClock>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CivicSignalDbContext>();

        return services;
    }

    private static void RegisterCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redisSection = configuration.GetSection(CivicSignalRedisOptions.SectionName);
        var redisEnabled = redisSection.GetValue<bool>(nameof(CivicSignalRedisOptions.Enabled));
        if (!redisEnabled)
        {
            return;
        }

        var connectionString = redisSection[nameof(CivicSignalRedisOptions.ConnectionString)]
            ?? "localhost:6379";
        var instanceName = redisSection[nameof(CivicSignalRedisOptions.InstanceName)]
            ?? "CivicSignal:";

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = instanceName;
        });
        services.AddScoped<IApplicationCache, RedisApplicationCache>();
    }

    private static void RegisterFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetSection(LocalFileStorageOptions.SectionName)
            [nameof(LocalFileStorageOptions.Provider)];

        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "MinIO", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAmazonS3>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<S3FileStorageOptions>>().Value;
                var config = new AmazonS3Config
                {
                    ForcePathStyle = options.ForcePathStyle
                };

                if (!string.IsNullOrWhiteSpace(options.Endpoint))
                {
                    config.ServiceURL = options.Endpoint.Trim();
                    config.UseHttp = config.ServiceURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                }
                else if (!string.IsNullOrWhiteSpace(options.Region))
                {
                    config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region.Trim());
                }

                return new AmazonS3Client(
                    new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                    config);
            });
            services.AddScoped<IFileStorageService, S3FileStorageService>();
            return;
        }

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }

    private static void RegisterMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqEnabled = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .GetValue<bool>(nameof(RabbitMqOptions.Enabled));

        if (rabbitMqEnabled)
        {
            services.AddScoped<IIncidentProcessingQueue, RabbitMqIncidentProcessingQueue>();
            services.AddScoped<IDataImportJobQueue, RabbitMqDataImportJobQueue>();
        }
    }

    private static void RegisterWeather(IServiceCollection services, IConfiguration configuration)
    {
        var weatherSection = configuration.GetSection(WeatherOptions.SectionName);
        var weatherEnabled = weatherSection.GetValue<bool>(nameof(WeatherOptions.Enabled));
        if (!weatherEnabled)
        {
            return;
        }

        services.AddHttpClient<IWeatherService, NationalWeatherServiceWeatherService>(ConfigureWeatherHttpClient);
    }

    private static void RegisterGeocoding(IServiceCollection services, IConfiguration configuration)
    {
        var geocodingSection = configuration.GetSection(NominatimOptions.SectionName);
        var geocodingEnabled = geocodingSection.GetValue<bool>(nameof(NominatimOptions.Enabled));
        if (!geocodingEnabled)
        {
            return;
        }

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(ConfigureNominatimHttpClient);
    }

    private static void RegisterConfiguredIncidentAnalyzer(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var openAiSection = configuration.GetSection(OpenAiOptions.SectionName);
        var openAiEnabled = openAiSection.GetValue<bool>(nameof(OpenAiOptions.Enabled));
        var openAiApiKey = openAiSection[nameof(OpenAiOptions.ApiKey)] ?? configuration["OPENAI_API_KEY"];
        if (openAiEnabled && !string.IsNullOrWhiteSpace(openAiApiKey))
        {
            services
                .AddHttpClient<OpenAiIncidentAnalyzer>((provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
                    client.Timeout = options.RequestTimeout;
                });
            services.AddScoped<IAiIncidentAnalyzer>(provider =>
                provider.GetRequiredService<OpenAiIncidentAnalyzer>());
        }
        else
        {
            services.AddScoped<IAiIncidentAnalyzer>(provider =>
                provider.GetRequiredService<HeuristicIncidentAnalyzer>());
        }
    }

    private static void ConfigureAiServiceHttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = options.RequestTimeout;
    }

    private static void ConfigureNyc311HttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<Nyc311Options>>().Value;
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120));
    }

    private static void ConfigureWeatherHttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = options.RequestTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json");
    }

    private static void ConfigureNominatimHttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = options.RequestTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}
