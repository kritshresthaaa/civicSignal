using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Domain.Incidents;
using CivicSignal.Domain.Incidents.ValueObjects;
using CivicSignal.Infrastructure.Identity;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace CivicSignal.Infrastructure.DemoData;

public static class DemoDataSeeder
{
    private const string DemoPrefix = "[DEMO]";
    private static readonly Guid FallbackReviewerId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static async Task SeedDemoDataAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration
            .GetSection(DemoDataSeedOptions.SectionName)
            .Get<DemoDataSeedOptions>() ?? new DemoDataSeedOptions();

        if (!options.Enabled)
        {
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            if (options.DevelopmentOnly && !environment.IsDevelopment())
            {
                logger.LogInformation("Skipping demo data seed outside Development environment.");
                return;
            }

            var dbContext = scope.ServiceProvider.GetRequiredService<CivicSignalDbContext>();
            var existingDemoData = await dbContext.Incidents
                .AnyAsync(incident => incident.Description.StartsWith(DemoPrefix), cancellationToken);

            if (existingDemoData)
            {
                logger.LogInformation("Demo data seed skipped because demo incidents already exist.");
                return;
            }

            var textEmbeddings = scope.ServiceProvider.GetRequiredService<ITextEmbeddingGenerator>();
            var reviewerId = await ResolveReviewerIdAsync(scope.ServiceProvider);
            var incidents = CreateDemoIncidents(reviewerId);

            dbContext.Incidents.AddRange(incidents);

            foreach (var incident in incidents)
            {
                var embedding = await textEmbeddings.GenerateEmbeddingAsync(incident.Description, cancellationToken);
                dbContext.Entry(incident).Property("TextEmbedding").CurrentValue = new Vector(embedding);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {IncidentCount} CivicSignal demo incidents.", incidents.Count);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Demo data seed was skipped. Ensure PostgreSQL is running and migrations are applied.");
        }
    }

    private static IReadOnlyCollection<Incident> CreateDemoIncidents(Guid reviewerId)
    {
        var now = DateTimeOffset.UtcNow;

        var pothole = Incident.Create(
            $"{DemoPrefix} Large pothole with standing water on Pine Street near the bus stop.",
            new GeoPoint(40.71280, -74.00600),
            now.AddHours(-5));
        AddCompletedStep(pothole, "Geocoding", now.AddHours(-4).AddMinutes(-50));
        AddCompletedStep(pothole, "MediaAnalysis", now.AddHours(-4).AddMinutes(-45));
        AddCompletedStep(pothole, "DuplicateCheck", now.AddHours(-4).AddMinutes(-42));
        pothole.StartProcessingStep("TriageDraft", now.AddHours(-4).AddMinutes(-38));
        pothole.AddMedia(
            "pine-street-pothole.jpg",
            "image/jpeg",
            "placeholder://incident-media/demo/pine-street-pothole.jpg",
            now.AddHours(-5).AddMinutes(2));
        var potholePrediction = pothole.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.93),
            new AgencyCode("DOT"),
            "High confidence road damage report routed to DOT for urgent lane inspection.",
            "civicsignal-demo-analyzer",
            "demo-v1",
            "triage-demo-v1",
            142,
            now.AddHours(-4).AddMinutes(-37));
        potholePrediction.AddEvidence(
            "Text",
            "Road damage keywords",
            "Description mentions a large pothole and standing water near a transit stop.",
            0.94,
            now.AddHours(-4).AddMinutes(-37));
        potholePrediction.AddEvidence(
            "Routing",
            "Agency rule",
            "Road surface damage is mapped to the transportation agency code DOT.",
            0.90,
            now.AddHours(-4).AddMinutes(-36));
        pothole.CompleteProcessingStep("TriageDraft", now.AddHours(-4).AddMinutes(-35));

        var duplicatePothole = Incident.Create(
            $"{DemoPrefix} Deep pothole blocking the curb lane beside Pine Street bus stop after rain.",
            new GeoPoint(40.71284, -74.00603),
            now.AddHours(-3).AddMinutes(-20));
        AddCompletedStep(duplicatePothole, "Geocoding", now.AddHours(-3).AddMinutes(-15));
        AddCompletedStep(duplicatePothole, "MediaAnalysis", now.AddHours(-3).AddMinutes(-12));
        duplicatePothole.StartProcessingStep("DuplicateCheck", now.AddHours(-3).AddMinutes(-10));
        duplicatePothole.AddDuplicateCandidate(
            pothole.Id,
            new ConfidenceScore(0.91),
            "Text embedding and 6-meter geospatial distance match an existing Pine Street pothole report.",
            now.AddHours(-3).AddMinutes(-9));
        duplicatePothole.CompleteProcessingStep("DuplicateCheck", now.AddHours(-3).AddMinutes(-8));
        duplicatePothole.StartProcessingStep("TriageDraft", now.AddHours(-3).AddMinutes(-6));
        var duplicatePrediction = duplicatePothole.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.High,
            new ConfidenceScore(0.88),
            new AgencyCode("DOT"),
            "Likely duplicate road damage report near the Pine Street bus stop.",
            "civicsignal-demo-analyzer",
            "demo-v1",
            "triage-demo-v1",
            118,
            now.AddHours(-3).AddMinutes(-5));
        duplicatePrediction.AddEvidence(
            "Duplicate",
            "Nearby similar incident",
            "Found a prior report with similar wording inside the configured geospatial radius.",
            0.91,
            now.AddHours(-3).AddMinutes(-5));
        duplicatePothole.CompleteProcessingStep("TriageDraft", now.AddHours(-3).AddMinutes(-4));

        var flooding = Incident.Create(
            $"{DemoPrefix} Water pooling around a blocked storm drain outside City Hall.",
            new GeoPoint(40.71321, -74.00555),
            now.AddHours(-2).AddMinutes(-15));
        AddCompletedStep(flooding, "Geocoding", now.AddHours(-2).AddMinutes(-10));
        AddCompletedStep(flooding, "MediaAnalysis", now.AddHours(-2).AddMinutes(-8));
        flooding.StartProcessingStep("TriageDraft", now.AddHours(-2).AddMinutes(-6));
        var floodingPrediction = flooding.AddTriagePrediction(
            new IncidentCategory("Drainage"),
            IncidentSeverity.Medium,
            new ConfidenceScore(0.64),
            new AgencyCode("DPW"),
            "Medium confidence drainage issue routed to public works, flagged for human review.",
            "civicsignal-demo-analyzer",
            "demo-v1",
            "triage-demo-v1",
            167,
            now.AddHours(-2).AddMinutes(-5));
        floodingPrediction.AddEvidence(
            "Text",
            "Drainage wording",
            "Description mentions water pooling and a blocked storm drain.",
            0.72,
            now.AddHours(-2).AddMinutes(-5));
        floodingPrediction.AddEvidence(
            "Threshold",
            "Review required",
            "Confidence is below the automatic approval threshold.",
            0.64,
            now.AddHours(-2).AddMinutes(-4));
        flooding.FailProcessingStep("TriageDraft", "Confidence below threshold; manual verification required.", now.AddHours(-2).AddMinutes(-3));

        var reviewed = Incident.Create(
            $"{DemoPrefix} Broken sidewalk panel first reported as a pothole near Market Plaza.",
            new GeoPoint(40.71420, -74.00710),
            now.AddHours(-1).AddMinutes(-40));
        AddCompletedStep(reviewed, "Geocoding", now.AddHours(-1).AddMinutes(-35));
        AddCompletedStep(reviewed, "MediaAnalysis", now.AddHours(-1).AddMinutes(-30));
        reviewed.StartProcessingStep("TriageDraft", now.AddHours(-1).AddMinutes(-25));
        var reviewedPrediction = reviewed.AddTriagePrediction(
            new IncidentCategory("RoadDamage"),
            IncidentSeverity.Medium,
            new ConfidenceScore(0.76),
            new AgencyCode("DOT"),
            "Possible road damage, but sidewalk references suggest reviewer confirmation is needed.",
            "civicsignal-demo-analyzer",
            "demo-v1",
            "triage-demo-v1",
            155,
            now.AddHours(-1).AddMinutes(-24));
        reviewedPrediction.AddEvidence(
            "Text",
            "Ambiguous surface",
            "The report says pothole but also mentions a sidewalk panel near a plaza.",
            0.76,
            now.AddHours(-1).AddMinutes(-24));
        reviewed.CompleteProcessingStep("TriageDraft", now.AddHours(-1).AddMinutes(-22));
        reviewed.Review(
            ReviewDecision.Approved,
            reviewerId,
            "Reviewer corrected the asset type and routed to public works.",
            now.AddHours(-1).AddMinutes(-10),
            new IncidentCategory("Sidewalk"),
            new AgencyCode("DPW"),
            IncidentSeverity.Medium,
            duplicateOfIncidentId: null,
            acceptedPrediction: false);

        return [pothole, duplicatePothole, flooding, reviewed];
    }

    private static void AddCompletedStep(Incident incident, string stepName, DateTimeOffset startedAt)
    {
        incident.StartProcessingStep(stepName, startedAt);
        incident.CompleteProcessingStep(stepName, startedAt.AddMinutes(1));
    }

    private static async Task<Guid> ResolveReviewerIdAsync(IServiceProvider services)
    {
        var userManager = services.GetService<UserManager<ApplicationUser>>();
        if (userManager is null)
        {
            return FallbackReviewerId;
        }

        var operatorUser = await userManager.FindByEmailAsync("operator@civicsignal.local");
        if (operatorUser is not null)
        {
            return operatorUser.Id;
        }

        var adminUser = await userManager.FindByEmailAsync("admin@civicsignal.local");
        return adminUser?.Id ?? FallbackReviewerId;
    }
}
