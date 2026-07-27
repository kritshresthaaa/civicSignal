using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class ProcessingStepConfiguration : IEntityTypeConfiguration<ProcessingStep>
{
    public void Configure(EntityTypeBuilder<ProcessingStep> builder)
    {
        builder.ToTable("incident_processing_steps");

        builder.HasKey(step => step.Id);

        builder.Property(step => step.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(step => step.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(step => step.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(step => step.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(step => step.StartedAt)
            .HasColumnName("started_at");

        builder.Property(step => step.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(step => step.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2_000);

        builder.Property(step => step.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(step => new { step.IncidentId, step.Name })
            .HasDatabaseName("ix_incident_processing_steps_incident_id_name")
            .IsUnique();
    }
}
