using CivicSignal.Domain.HistoricalComplaints;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class HistoricalComplaintConfiguration : IEntityTypeConfiguration<HistoricalComplaint>
{
    public void Configure(EntityTypeBuilder<HistoricalComplaint> builder)
    {
        builder.ToTable("historical_complaints");

        builder.HasKey(complaint => complaint.Id);

        builder.Property(complaint => complaint.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(complaint => complaint.Source)
            .HasColumnName("source")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(complaint => complaint.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(complaint => complaint.Category)
            .HasColumnName("category")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(complaint => complaint.ComplaintType)
            .HasColumnName("complaint_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(complaint => complaint.Descriptor)
            .HasColumnName("descriptor")
            .HasMaxLength(300);

        builder.Property(complaint => complaint.Agency)
            .HasColumnName("agency")
            .HasMaxLength(40);

        builder.Property(complaint => complaint.AgencyName)
            .HasColumnName("agency_name")
            .HasMaxLength(200);

        builder.Property(complaint => complaint.Status)
            .HasColumnName("status")
            .HasMaxLength(80);

        builder.Property(complaint => complaint.Borough)
            .HasColumnName("borough")
            .HasMaxLength(80);

        builder.Property(complaint => complaint.IncidentAddress)
            .HasColumnName("incident_address")
            .HasMaxLength(300);

        builder.Property(complaint => complaint.ResolutionDescription)
            .HasColumnName("resolution_description")
            .HasMaxLength(2_000);

        builder.ComplexProperty(complaint => complaint.Location, location =>
        {
            location.Property(point => point.Latitude)
                .HasColumnName("latitude")
                .IsRequired();

            location.Property(point => point.Longitude)
                .HasColumnName("longitude")
                .IsRequired();
        });

        builder.Property<Point>("LocationPoint")
            .HasColumnName("location")
            .HasColumnType("geography(point,4326)")
            .HasComputedColumnSql("ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography", stored: true)
            .IsRequired();

        builder.Property(complaint => complaint.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(complaint => complaint.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(complaint => complaint.ImportedAt)
            .HasColumnName("imported_at")
            .IsRequired();

        builder.Property(complaint => complaint.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(complaint => new { complaint.Source, complaint.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_historical_complaints_source_external_id");

        builder.HasIndex(complaint => complaint.Category)
            .HasDatabaseName("ix_historical_complaints_category");

        builder.HasIndex(complaint => complaint.ComplaintType)
            .HasDatabaseName("ix_historical_complaints_complaint_type");

        builder.HasIndex(complaint => complaint.Agency)
            .HasDatabaseName("ix_historical_complaints_agency");

        builder.HasIndex(complaint => complaint.Status)
            .HasDatabaseName("ix_historical_complaints_status");

        builder.HasIndex(complaint => complaint.Borough)
            .HasDatabaseName("ix_historical_complaints_borough");

        builder.HasIndex(complaint => complaint.CreatedAt)
            .HasDatabaseName("ix_historical_complaints_created_at");

        builder.HasIndex("LocationPoint")
            .HasDatabaseName("ix_historical_complaints_location")
            .HasMethod("gist");
    }
}
