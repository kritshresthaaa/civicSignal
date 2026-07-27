using CivicSignal.Domain.DataImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class DataImportJobConfiguration : IEntityTypeConfiguration<DataImportJob>
{
    public void Configure(EntityTypeBuilder<DataImportJob> builder)
    {
        builder.ToTable("data_import_jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(job => job.Source)
            .HasColumnName("source")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(job => job.ImportType)
            .HasColumnName("import_type")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(job => job.ParametersJson)
            .HasColumnName("parameters_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(job => job.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(job => job.RequestedByUserId)
            .HasColumnName("requested_by_user_id");

        builder.Property(job => job.RequestedAt)
            .HasColumnName("requested_at")
            .IsRequired();

        builder.Property(job => job.StartedAt)
            .HasColumnName("started_at");

        builder.Property(job => job.FinishedAt)
            .HasColumnName("finished_at");

        builder.Property(job => job.ReceivedCount)
            .HasColumnName("received_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(job => job.CreatedCount)
            .HasColumnName("created_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(job => job.UpdatedCount)
            .HasColumnName("updated_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(job => job.SkippedCount)
            .HasColumnName("skipped_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(job => job.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1_000);

        builder.Property(job => job.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(job => job.Status)
            .HasDatabaseName("ix_data_import_jobs_status");

        builder.HasIndex(job => job.Source)
            .HasDatabaseName("ix_data_import_jobs_source");

        builder.HasIndex(job => job.RequestedAt)
            .HasDatabaseName("ix_data_import_jobs_requested_at");

        builder.HasIndex(job => new { job.Source, job.Status, job.RequestedAt })
            .HasDatabaseName("ix_data_import_jobs_source_status_requested_at");
    }
}
