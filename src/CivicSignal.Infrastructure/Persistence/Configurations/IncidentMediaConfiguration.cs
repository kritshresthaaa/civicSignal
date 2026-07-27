using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class IncidentMediaConfiguration : IEntityTypeConfiguration<IncidentMedia>
{
    public void Configure(EntityTypeBuilder<IncidentMedia> builder)
    {
        builder.ToTable("incident_media");

        builder.HasKey(media => media.Id);

        builder.Property(media => media.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(media => media.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(media => media.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(media => media.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(media => media.StorageUri)
            .HasColumnName("storage_uri")
            .HasMaxLength(2_048)
            .IsRequired();

        builder.Property(media => media.MediaType)
            .HasColumnName("media_type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(media => media.AnalysisStatus)
            .HasColumnName("analysis_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasDefaultValue(IncidentMediaAnalysisStatus.Pending)
            .IsRequired();

        builder.Property(media => media.AnalysisSummary)
            .HasColumnName("analysis_summary")
            .HasMaxLength(1_000);

        builder.Property(media => media.Transcript)
            .HasColumnName("transcript")
            .HasMaxLength(4_000);

        builder.Property(media => media.DetectedLabels)
            .HasColumnName("detected_labels")
            .HasMaxLength(1_000);

        builder.Property(media => media.AnalysisConfidence)
            .HasColumnName("analysis_confidence");

        builder.Property(media => media.AnalysisModelName)
            .HasColumnName("analysis_model_name")
            .HasMaxLength(160);

        builder.Property(media => media.AnalysisModelVersion)
            .HasColumnName("analysis_model_version")
            .HasMaxLength(80);

        builder.Property(media => media.AnalysisProcessingTimeMilliseconds)
            .HasColumnName("analysis_processing_time_milliseconds");

        builder.Property(media => media.AnalysisError)
            .HasColumnName("analysis_error")
            .HasMaxLength(1_000);

        builder.Property(media => media.AnalyzedAt)
            .HasColumnName("analyzed_at");

        builder.Property(media => media.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(media => media.IncidentId)
            .HasDatabaseName("ix_incident_media_incident_id");
    }
}
