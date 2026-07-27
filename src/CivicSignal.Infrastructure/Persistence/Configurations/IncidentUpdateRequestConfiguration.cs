using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class IncidentUpdateRequestConfiguration : IEntityTypeConfiguration<IncidentUpdateRequest>
{
    public void Configure(EntityTypeBuilder<IncidentUpdateRequest> builder)
    {
        builder.ToTable("incident_update_requests");

        builder.HasKey(updateRequest => updateRequest.Id);

        builder.Property(updateRequest => updateRequest.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(updateRequest => updateRequest.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        builder.Property(updateRequest => updateRequest.Message)
            .HasColumnName("message")
            .HasMaxLength(2_000)
            .IsRequired();

        builder.Property(updateRequest => updateRequest.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(updateRequest => updateRequest.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(updateRequest => new { updateRequest.IncidentId, updateRequest.CreatedAt })
            .HasDatabaseName("ix_incident_update_requests_incident_id_created_at");
    }
}
