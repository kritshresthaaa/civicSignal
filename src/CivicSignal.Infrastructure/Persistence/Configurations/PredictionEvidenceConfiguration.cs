using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class PredictionEvidenceConfiguration : IEntityTypeConfiguration<PredictionEvidence>
{
    public void Configure(EntityTypeBuilder<PredictionEvidence> builder)
    {
        builder.ToTable("prediction_evidence");

        builder.HasKey(evidence => evidence.Id);

        builder.Property(evidence => evidence.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(evidence => evidence.TriagePredictionId)
            .HasColumnName("triage_prediction_id")
            .IsRequired();

        builder.Property(evidence => evidence.Kind)
            .HasColumnName("kind")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(evidence => evidence.Title)
            .HasColumnName("title")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(evidence => evidence.Detail)
            .HasColumnName("detail")
            .HasMaxLength(1_000)
            .IsRequired();

        builder.Property(evidence => evidence.Confidence)
            .HasColumnName("confidence");

        builder.Property(evidence => evidence.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(evidence => new { evidence.TriagePredictionId, evidence.CreatedAt })
            .HasDatabaseName("ix_prediction_evidence_triage_prediction_id_created_at");
    }
}
