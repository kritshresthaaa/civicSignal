using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class TriagePredictionConfiguration : IEntityTypeConfiguration<TriagePrediction>
{
    public void Configure(EntityTypeBuilder<TriagePrediction> builder)
    {
        builder.ToTable("triage_predictions");

        builder.HasKey(prediction => prediction.Id);

        builder.Property(prediction => prediction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(prediction => prediction.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.ComplexProperty(prediction => prediction.Category, category =>
        {
            category.Property(value => value.Value)
                .HasColumnName("category")
                .HasMaxLength(80)
                .IsRequired();
        });

        builder.Property(prediction => prediction.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.ComplexProperty(prediction => prediction.Confidence, confidence =>
        {
            confidence.Property(score => score.Value)
                .HasColumnName("confidence")
                .IsRequired();
        });

        builder.Property(prediction => prediction.Summary)
            .HasColumnName("summary")
            .HasMaxLength(2_000)
            .IsRequired();

        builder.ComplexProperty(prediction => prediction.SuggestedAgency, agency =>
        {
            agency.Property(value => value.Value)
                .HasColumnName("suggested_agency_code")
                .HasMaxLength(32)
                .IsRequired();
        });

        builder.Property(prediction => prediction.ModelName)
            .HasColumnName("model_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(prediction => prediction.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(80);

        builder.Property(prediction => prediction.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(80);

        builder.Property(prediction => prediction.ProcessingTimeMilliseconds)
            .HasColumnName("processing_time_ms");

        builder.Property(prediction => prediction.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(prediction => new { prediction.IncidentId, prediction.CreatedAt })
            .HasDatabaseName("ix_triage_predictions_incident_id_created_at");

        builder.HasMany(prediction => prediction.EvidenceItems)
            .WithOne()
            .HasForeignKey(evidence => evidence.TriagePredictionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(prediction => prediction.EvidenceItems)
            .HasField("_evidenceItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
