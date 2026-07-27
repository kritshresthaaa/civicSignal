using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Domain.DataImports;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents;
using CivicSignal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.Persistence;

public sealed class CivicSignalDbContext(DbContextOptions<CivicSignalDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext, IUnitOfWork
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<DataImportJob> DataImportJobs => Set<DataImportJob>();

    public DbSet<HistoricalComplaint> HistoricalComplaints => Set<HistoricalComplaint>();

    public DbSet<IncidentMedia> IncidentMedia => Set<IncidentMedia>();

    public DbSet<TriagePrediction> TriagePredictions => Set<TriagePrediction>();

    public DbSet<PredictionEvidence> PredictionEvidence => Set<PredictionEvidence>();

    public DbSet<DuplicateCandidate> DuplicateCandidates => Set<DuplicateCandidate>();

    public DbSet<IncidentReviewRecord> IncidentReviewRecords => Set<IncidentReviewRecord>();

    public DbSet<IncidentUpdateRequest> IncidentUpdateRequests => Set<IncidentUpdateRequest>();

    public DbSet<IncidentFeedback> IncidentFeedback => Set<IncidentFeedback>();

    public DbSet<ApplicationRefreshToken> RefreshTokens => Set<ApplicationRefreshToken>();

    IQueryable<Incident> IApplicationDbContext.Incidents => Incidents;

    IQueryable<DataImportJob> IApplicationDbContext.DataImportJobs => DataImportJobs;

    IQueryable<HistoricalComplaint> IApplicationDbContext.HistoricalComplaints => HistoricalComplaints;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("vector");
        ConfigureIdentityTables(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CivicSignalDbContext).Assembly);
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("identity_users");
        modelBuilder.Entity<ApplicationRole>().ToTable("identity_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");

        modelBuilder.Entity<ApplicationRole>().HasData(IdentitySeedData.Roles);
    }
}
