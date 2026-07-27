using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class IncidentFeedbackConfiguration : IEntityTypeConfiguration<IncidentFeedback>
{
    public void Configure(EntityTypeBuilder<IncidentFeedback> builder)
    {
        builder.ToTable("incident_feedback", table => table.HasCheckConstraint(
            "ck_incident_feedback_rating_range",
            "rating >= 1 AND rating <= 5"));

        builder.HasKey(feedback => feedback.Id);

        builder.Property(feedback => feedback.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(feedback => feedback.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(feedback => feedback.Comment)
            .HasColumnName("comment")
            .HasMaxLength(2_000);

        builder.Property(feedback => feedback.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(feedback => new { feedback.IncidentId, feedback.CreatedAt })
            .HasDatabaseName("ix_incident_feedback_incident_id_created_at");
    }
}
