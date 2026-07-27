using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class DuplicateCandidateConfiguration : IEntityTypeConfiguration<DuplicateCandidate>
{
    public void Configure(EntityTypeBuilder<DuplicateCandidate> builder)
    {
        builder.ToTable("duplicate_candidates");

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(candidate => candidate.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(candidate => candidate.CandidateIncidentId)
            .HasColumnName("candidate_incident_id")
            .IsRequired();

        builder.ComplexProperty(candidate => candidate.SimilarityScore, similarity =>
        {
            similarity.Property(score => score.Value)
                .HasColumnName("similarity_score")
                .IsRequired();
        });

        builder.Property(candidate => candidate.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1_000);

        builder.Property(candidate => candidate.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(candidate => candidate.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(candidate => candidate.CandidateIncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(candidate => new { candidate.IncidentId, candidate.CandidateIncidentId })
            .HasDatabaseName("ix_duplicate_candidates_incident_id_candidate_incident_id")
            .IsUnique();
    }
}
