using Callora.Core.Domain.Surfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class SurfaceHandoffTicketRecordEntityTypeConfiguration
    : IEntityTypeConfiguration<SurfaceHandoffTicketRecord>
{
    public void Configure(EntityTypeBuilder<SurfaceHandoffTicketRecord> builder)
    {
        builder.ToTable("surface_handoff_tickets");
        builder.HasKey(x => x.Id);

        // Redemption looks the ticket up by hash and nothing else, and the uniqueness
        // is what makes the delete-and-return single-use rather than best effort.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TenantKey).HasColumnName("tenant_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasColumnName("workspace_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SourceSurfaceKey).HasColumnName("source_surface_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.TargetSurfaceKey).HasColumnName("target_surface_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.TargetAudience).HasColumnName("target_audience").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClaimsJson).HasColumnName("claims_json").IsRequired();
        builder.Property(x => x.AuthenticationMethod).HasColumnName("authentication_method").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AuthenticatedAtUtc).HasColumnName("authenticated_at_utc").IsRequired();
        builder.Property(x => x.IdentityExpiresAtUtc).HasColumnName("identity_expires_at_utc").IsRequired();
        builder.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
    }
}
