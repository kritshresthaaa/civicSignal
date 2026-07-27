using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class IncidentReviewRecordConfiguration : IEntityTypeConfiguration<IncidentReviewRecord>
{
    public void Configure(EntityTypeBuilder<IncidentReviewRecord> builder)
    {
        builder.ToTable("incident_review_records");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(review => review.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(review => review.Decision)
            .HasColumnName("decision")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(review => review.ReviewerUserId)
            .HasColumnName("reviewer_user_id")
            .IsRequired();

        builder.Property(review => review.Note)
            .HasColumnName("note")
            .HasMaxLength(2_000);

        builder.Property(review => review.CorrectedCategory)
            .HasColumnName("corrected_category")
            .HasMaxLength(80);

        builder.Property(review => review.CorrectedAgencyCode)
            .HasColumnName("corrected_agency_code")
            .HasMaxLength(32);

        builder.Property(review => review.CorrectedSeverity)
            .HasColumnName("corrected_severity")
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(review => review.DuplicateOfIncidentId)
            .HasColumnName("duplicate_of_incident_id");

        builder.Property(review => review.AcceptedPrediction)
            .HasColumnName("accepted_prediction");

        builder.Property(review => review.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(review => new { review.IncidentId, review.CreatedAt })
            .HasDatabaseName("ix_incident_review_records_incident_id_created_at");
    }
}
