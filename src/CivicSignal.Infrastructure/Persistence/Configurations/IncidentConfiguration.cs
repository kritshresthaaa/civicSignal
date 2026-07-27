using CivicSignal.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using Pgvector;

namespace CivicSignal.Infrastructure.Persistence.Configurations;

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(incident => incident.Id);

        builder.Property(incident => incident.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(incident => incident.Description)
            .HasColumnName("description")
            .HasMaxLength(2_000)
            .IsRequired();

        builder.Property(incident => incident.PublicTrackingCode)
            .HasColumnName("public_tracking_code")
            .HasMaxLength(12)
            .IsRequired();

        builder.Property(incident => incident.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.ComplexProperty(incident => incident.Location, location =>
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

        builder.Property<Vector?>("TextEmbedding")
            .HasColumnName("text_embedding")
            .HasColumnType("vector(1024)");

        builder.Property(incident => incident.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(incident => incident.ReviewDecision)
            .HasColumnName("review_decision")
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(incident => incident.ReviewNote)
            .HasColumnName("review_note")
            .HasMaxLength(2_000);

        builder.Property(incident => incident.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");

        builder.Property(incident => incident.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(incident => incident.CorrectedCategory)
            .HasColumnName("corrected_category")
            .HasMaxLength(80);

        builder.Property(incident => incident.CorrectedAgencyCode)
            .HasColumnName("corrected_agency_code")
            .HasMaxLength(32);

        builder.Property(incident => incident.CorrectedSeverity)
            .HasColumnName("corrected_severity")
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(incident => incident.DuplicateOfIncidentId)
            .HasColumnName("duplicate_of_incident_id");

        builder.Property(incident => incident.AssignedAgencyCode)
            .HasColumnName("assigned_agency_code")
            .HasMaxLength(32);

        builder.Property(incident => incident.AssignedTeam)
            .HasColumnName("assigned_team")
            .HasMaxLength(160);

        builder.Property(incident => incident.AssignedByUserId)
            .HasColumnName("assigned_by_user_id");

        builder.Property(incident => incident.AssignedAt)
            .HasColumnName("assigned_at");

        builder.Property(incident => incident.DispatchedByUserId)
            .HasColumnName("dispatched_by_user_id");

        builder.Property(incident => incident.DispatchedAt)
            .HasColumnName("dispatched_at");

        builder.Property(incident => incident.DuplicateLinkedByUserId)
            .HasColumnName("duplicate_linked_by_user_id");

        builder.Property(incident => incident.DuplicateLinkedAt)
            .HasColumnName("duplicate_linked_at");

        builder.Property(incident => incident.AcceptedPrediction)
            .HasColumnName("accepted_prediction");

        builder.Property(incident => incident.NotificationAlertsEnabled)
            .HasColumnName("notification_alerts_enabled")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(incident => incident.NotificationChannel)
            .HasColumnName("notification_channel")
            .HasMaxLength(80)
            .HasDefaultValue("None")
            .IsRequired();

        builder.Property(incident => incident.NotificationPreferenceUpdatedAt)
            .HasColumnName("notification_preference_updated_at");

        builder.HasMany(incident => incident.ProcessingSteps)
            .WithOne()
            .HasForeignKey(step => step.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.ProcessingSteps)
            .HasField("_processingSteps")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.MediaItems)
            .WithOne()
            .HasForeignKey(media => media.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.MediaItems)
            .HasField("_mediaItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.TriagePredictions)
            .WithOne()
            .HasForeignKey(prediction => prediction.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.TriagePredictions)
            .HasField("_triagePredictions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.DuplicateCandidates)
            .WithOne()
            .HasForeignKey(candidate => candidate.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.DuplicateCandidates)
            .HasField("_duplicateCandidates")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.ReviewRecords)
            .WithOne()
            .HasForeignKey(review => review.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.ReviewRecords)
            .HasField("_reviewRecords")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.UpdateRequests)
            .WithOne()
            .HasForeignKey(updateRequest => updateRequest.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.UpdateRequests)
            .HasField("_updateRequests")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(incident => incident.FeedbackItems)
            .WithOne()
            .HasForeignKey(feedback => feedback.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(incident => incident.FeedbackItems)
            .HasField("_feedbackItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex("LocationPoint")
            .HasDatabaseName("ix_incidents_location")
            .HasMethod("gist");

        builder.HasIndex(incident => incident.PublicTrackingCode)
            .IsUnique()
            .HasDatabaseName("ix_incidents_public_tracking_code");

        builder.HasIndex("TextEmbedding")
            .HasDatabaseName("ix_incidents_text_embedding_hnsw")
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);
    }
}
