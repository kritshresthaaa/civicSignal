using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Application.Abstractions.Geocoding;
using CivicSignal.Application.Abstractions.Storage;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.Identity;
using CivicSignal.Application.Abstractions.OpenData;
using CivicSignal.Domain.DataImports;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents;
using CivicSignal.Infrastructure.DependencyInjection;
using CivicSignal.Infrastructure.Identity;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.IntegrationTests;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_registers_repositories_and_identity_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGenericRepository<Incident>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIncidentRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGenericRepository<DataImportJob>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDataImportJobRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGenericRepository<HistoricalComplaint>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IHistoricalComplaintRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAiIncidentAnalyzer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIncidentMediaAnalyzer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITextEmbeddingGenerator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDuplicateIncidentSearchService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INyc311ComplaintClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGeocodingService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWeatherService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFileStorageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAuthService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>());
    }

    [Fact]
    public void AddInfrastructure_can_register_redis_cache_adapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Redis:InstanceName"] = "CivicSignalTests:"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        Assert.Equal("RedisApplicationCache", cache.GetType().Name);
    }

    [Fact]
    public void AddInfrastructure_can_register_s3_object_storage_adapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["FileStorage:Provider"] = "S3",
                ["S3Storage:Endpoint"] = "http://localhost:9000",
                ["S3Storage:AccessKey"] = "minioadmin",
                ["S3Storage:SecretKey"] = "minioadmin",
                ["S3Storage:BucketName"] = "civic-signal",
                ["S3Storage:Region"] = "us-east-1",
                ["S3Storage:ForcePathStyle"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        Assert.Equal("S3FileStorageService", storage.GetType().Name);
    }

    [Fact]
    public void AddInfrastructure_can_register_rabbitmq_processing_queue_adapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["RabbitMq:Enabled"] = "true",
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IIncidentProcessingQueue>();
        var dataImportQueue = scope.ServiceProvider.GetRequiredService<IDataImportJobQueue>();

        Assert.Equal("RabbitMqIncidentProcessingQueue", queue.GetType().Name);
        Assert.Equal("RabbitMqDataImportJobQueue", dataImportQueue.GetType().Name);
    }

    [Fact]
    public void AddInfrastructure_can_register_python_ai_service_adapters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["AiService:Enabled"] = "true",
                ["AiService:BaseUrl"] = "http://localhost:8010",
                ["AiService:UseRemoteEmbeddings"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAiIncidentAnalyzer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIncidentMediaAnalyzer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITextEmbeddingGenerator>());
    }

    [Fact]
    public void AddInfrastructure_can_register_weather_adapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["Weather:Enabled"] = "true",
                ["Weather:BaseUrl"] = "https://api.weather.gov",
                ["Weather:UserAgent"] = "CivicSignalAI.Tests/0.1"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var weather = scope.ServiceProvider.GetRequiredService<IWeatherService>();

        Assert.Equal("NationalWeatherServiceWeatherService", weather.GetType().Name);
    }

    [Fact]
    public void AddInfrastructure_can_register_nominatim_geocoding_adapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["Nominatim:Enabled"] = "true",
                ["Nominatim:BaseUrl"] = "https://nominatim.openstreetmap.org",
                ["Nominatim:UserAgent"] = "CivicSignalAI.Tests/0.1"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

        Assert.Equal("NominatimGeocodingService", geocoding.GetType().Name);
    }

    [Fact]
    public void DbContext_model_seeds_default_identity_roles()
    {
        var options = new DbContextOptionsBuilder<CivicSignalDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.UseVector();
                })
            .Options;

        using var dbContext = new CivicSignalDbContext(options);
        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;
        var roleEntity = designTimeModel.FindEntityType(typeof(ApplicationRole));

        Assert.NotNull(roleEntity);

        var roleNames = roleEntity
            .GetSeedData()
            .Select(seed => Assert.IsType<string>(seed[nameof(ApplicationRole.Name)]))
            .ToArray();

        Assert.Contains("Administrator", roleNames);
        Assert.Contains("Operator", roleNames);
        Assert.Contains("Reviewer", roleNames);
        Assert.Contains("Reporter", roleNames);
    }

    [Fact]
    public void DbContext_model_maps_historical_complaints_with_postgis_location()
    {
        var options = new DbContextOptionsBuilder<CivicSignalDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.UseVector();
                })
            .Options;

        using var dbContext = new CivicSignalDbContext(options);
        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;
        var complaintEntity = designTimeModel.FindEntityType(typeof(HistoricalComplaint));

        Assert.NotNull(complaintEntity);
        Assert.Equal("historical_complaints", complaintEntity.GetTableName());

        var location = complaintEntity.FindProperty("LocationPoint");
        Assert.NotNull(location);
        Assert.Equal("geography(point,4326)", location.GetColumnType());

        Assert.Contains(complaintEntity.GetIndexes(), index =>
            index.GetDatabaseName() == "ux_historical_complaints_source_external_id" && index.IsUnique);
        Assert.Contains(complaintEntity.GetIndexes(), index =>
            index.GetDatabaseName() == "ix_historical_complaints_location");
    }

    [Fact]
    public void DbContext_model_maps_data_import_jobs()
    {
        var options = new DbContextOptionsBuilder<CivicSignalDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.UseVector();
                })
            .Options;

        using var dbContext = new CivicSignalDbContext(options);
        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;
        var jobEntity = designTimeModel.FindEntityType(typeof(DataImportJob));

        Assert.NotNull(jobEntity);
        Assert.Equal("data_import_jobs", jobEntity.GetTableName());
        Assert.Equal("jsonb", jobEntity.FindProperty(nameof(DataImportJob.ParametersJson))?.GetColumnType());
        Assert.Contains(jobEntity.GetIndexes(), index =>
            index.GetDatabaseName() == "ix_data_import_jobs_source_status_requested_at");
    }

    [Fact]
    public async Task Local_file_storage_stores_and_reopens_media()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"civicsignal-storage-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = "Host=localhost;Port=5432;Database=civic_signal_tests;Username=postgres;Password=postgres",
                ["FileStorage:RootPath"] = rootPath,
                ["FileStorage:PublicBasePath"] = "/media",
                ["FileStorage:MaxUploadBytes"] = "1024",
                ["FileStorage:AllowedContentTypes:0"] = "image/png"
            })
            .Build();

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
            services.AddInfrastructure(configuration);

            using var provider = services.BuildServiceProvider();
            var storage = provider.GetRequiredService<IFileStorageService>();
            var fileBytes = "fake-image-bytes"u8.ToArray();

            var stored = await storage.StoreAsync(
                new MemoryStream(fileBytes),
                "../street.png",
                "image/png",
                CancellationToken.None);

            await using var opened = await storage.OpenReadAsync(stored.StorageUri, CancellationToken.None);

            Assert.Equal("street.png", stored.FileName);
            Assert.Equal("image/png", stored.ContentType);
            Assert.StartsWith("/media/", stored.StorageUri, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(opened);

            using var memory = new MemoryStream();
            await opened.CopyToAsync(memory);
            Assert.Equal(fileBytes, memory.ToArray());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CivicSignal.Infrastructure.IntegrationTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
