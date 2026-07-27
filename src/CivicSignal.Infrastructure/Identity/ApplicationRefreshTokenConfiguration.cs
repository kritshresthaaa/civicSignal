using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicSignal.Infrastructure.Identity;

internal sealed class ApplicationRefreshTokenConfiguration : IEntityTypeConfiguration<ApplicationRefreshToken>
{
    public void Configure(EntityTypeBuilder<ApplicationRefreshToken> builder)
    {
        builder.ToTable("identity_refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(token => token.ReplacedByTokenHash)
            .HasColumnName("replaced_by_token_hash")
            .HasMaxLength(128);

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_identity_refresh_tokens_token_hash");

        builder.HasIndex(token => new { token.UserId, token.ExpiresAt })
            .HasDatabaseName("ix_identity_refresh_tokens_user_id_expires_at");
    }
}
