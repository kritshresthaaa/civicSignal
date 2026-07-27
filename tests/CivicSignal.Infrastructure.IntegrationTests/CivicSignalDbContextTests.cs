using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.Incidents;
using CivicSignal.Domain.DataImports;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents;
using CivicSignal.Domain.Incidents.ValueObjects;
using CivicSignal.Infrastructure.DependencyInjection;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CivicSignal.Infrastructure.IntegrationTests;

public sealed class CivicSignalDbContextTests
{
    [Fact]
    public async Task Migrations_apply_extensions_and_incident_round_trips_when_testcontainers_enabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRES_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var postgres = new PostgreSqlBuilder("datosonline/postgis-pgvector:latest")
            .WithDatabase("civic_signal_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<CivicSignalDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.UseVector();
                })
            .Options;

        await using var dbContext = new CivicSignalDbContext(options);
        await dbContext.Database.MigrateAsync();

        var extensionCount = await CountRequiredExtensions(dbContext);
        Assert.Equal(2, extensionCount);

        var incident = Incident.Create(
            "Large pothole near Main Street",
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.UtcNow);
        var duplicateIncident = Incident.Create(
            "Large pothole near Main Street crosswalk",
            new GeoPoint(40.7129, -74.0061),
            DateTimeOffset.UtcNow);

        dbContext.Incidents.Add(incident);
        dbContext.Incidents.Add(duplicateIncident);
        dbContext.HistoricalComplaints.Add(HistoricalComplaint.Create(
            HistoricalComplaint.Nyc311Source,
            "311-1",
            "RoadDamage",
            "Street Condition",
            "Pothole",
            "DOT",
            "Department of Transportation",
            "Closed",
            "MANHATTAN",
            "Main Street",
            "Work completed.",
            new GeoPoint(40.7127, -74.0061),
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow));
        dbContext.DataImportJobs.Add(DataImportJob.RequestNyc311HistoricalComplaints(
            """{"limit":100}""",
            null,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        incident.AddMedia(
            "main-street-pothole.jpg",
            "image/jpeg",
            "placeholder://incident-media/main-street-pothole.jpg",
            DateTimeOffset.UtcNow);
        incident.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.91),
            new AgencyCode("DOT"),
            "High RoadDamage report routed to DOT.",
            "test-analyzer",
            "1.0",
            "test-prompt-v1",
            100,
            DateTimeOffset.UtcNow);
        Assert.Single(incident.TriagePredictions).AddEvidence(
            "Text",
            "Category keyword match",
            "Matched category term(s): pothole.",
            0.91,
            DateTimeOffset.UtcNow);
        incident.AddDuplicateCandidate(
            duplicateIncident.Id,
            new ConfidenceScore(0.88),
            "Similar report text near the same coordinates.",
            DateTimeOffset.UtcNow);
        incident.RequestUpdate(
            "Could you notify me when a crew is assigned?",
            DateTimeOffset.UtcNow);
        incident.UpdateNotificationPreference(
            alertsEnabled: true,
            channel: "Browser",
            DateTimeOffset.UtcNow);
        incident.AddFeedback(
            5,
            "The status page was clear.",
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.Incidents
            .Include(saved => saved.MediaItems)
            .Include(saved => saved.TriagePredictions)
                .ThenInclude(prediction => prediction.EvidenceItems)
            .Include(saved => saved.DuplicateCandidates)
            .Include(saved => saved.UpdateRequests)
            .Include(saved => saved.FeedbackItems)
            .SingleAsync(saved => saved.Id == incident.Id);

        Assert.Equal("Large pothole near Main Street", loaded.Description);
        Assert.Equal(IncidentStatus.Submitted, loaded.Status);
        Assert.Equal(40.7128, loaded.Location.Latitude, precision: 4);
        Assert.Equal(-74.0060, loaded.Location.Longitude, precision: 4);
        Assert.Equal("Image", Assert.Single(loaded.MediaItems).MediaType.ToString());
        var prediction = Assert.Single(loaded.TriagePredictions);
        Assert.Equal("RoadDamage", prediction.Category.Value);
        Assert.Equal("test-prompt-v1", prediction.PromptVersion);
        Assert.Equal("Text", Assert.Single(prediction.EvidenceItems).Kind);
        Assert.Equal(duplicateIncident.Id, Assert.Single(loaded.DuplicateCandidates).CandidateIncidentId);
        Assert.Equal("Could you notify me when a crew is assigned?", Assert.Single(loaded.UpdateRequests).Message);
        Assert.True(loaded.NotificationAlertsEnabled);
        Assert.Equal("Browser", loaded.NotificationChannel);
        Assert.Equal(5, Assert.Single(loaded.FeedbackItems).Rating);

        var historicalComplaint = await dbContext.HistoricalComplaints.SingleAsync(saved => saved.ExternalId == "311-1");
        Assert.Equal("RoadDamage", historicalComplaint.Category);
        Assert.Equal("DOT", historicalComplaint.Agency);

        var importJob = await dbContext.DataImportJobs.SingleAsync();
        Assert.Equal(DataImportJobStatus.Pending, importJob.Status);
    }

    [Fact]
    public async Task Pgvector_duplicate_search_finds_nearby_similar_incident_when_testcontainers_enabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRES_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var postgres = new PostgreSqlBuilder("datosonline/postgis-pgvector:latest")
            .WithDatabase("civic_signal_duplicate_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CivicSignal"] = postgres.GetConnectionString(),
                ["FileStorage:RootPath"] = Path.Combine(Path.GetTempPath(), $"civicsignal-storage-{Guid.NewGuid():N}"),
                ["OpenAI:Enabled"] = "false",
                ["DuplicateDetection:SearchRadiusMeters"] = "500",
                ["DuplicateDetection:MinimumScore"] = "0.7"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<CivicSignalDbContext>();
        await dbContext.Database.MigrateAsync();

        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
        var intelligence = scope.ServiceProvider.GetRequiredService<IIncidentIntelligenceService>();

        var duplicate = await incidents.CreateAsync(
            new CreateIncidentInput("Large pothole blocking the right lane near Main Street", 40.7128, -74.0060),
            CancellationToken.None);
        var target = await incidents.CreateAsync(
            new CreateIncidentInput("Large pothole blocking the right lane near Main Street after rain", 40.71282, -74.00603),
            CancellationToken.None);
        var farAway = await incidents.CreateAsync(
            new CreateIncidentInput("Large pothole blocking the right lane near Main Street", 40.9228, -74.4060),
            CancellationToken.None);

        await intelligence.AnalyzeAsync(target.Id, CancellationToken.None);
        var candidates = await intelligence.GetDuplicateCandidatesAsync(target.Id, CancellationToken.None);

        Assert.NotNull(candidates);
        Assert.Contains(candidates, candidate => candidate.CandidateIncidentId == duplicate.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.CandidateIncidentId == farAway.Id);
    }

    private static async Task<long> CountRequiredExtensions(CivicSignalDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();

        await dbContext.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from pg_extension where extname in ('postgis', 'vector');";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
