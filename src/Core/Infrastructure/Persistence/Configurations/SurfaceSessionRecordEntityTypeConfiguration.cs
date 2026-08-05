using Callora.Core.Domain.Surfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class SurfaceSessionRecordEntityTypeConfiguration : IEntityTypeConfiguration<SurfaceSessionRecord>
{
    public void Configure(EntityTypeBuilder<SurfaceSessionRecord> builder)
    {
        builder.ToTable("surface_sessions");
        builder.HasKey(x => x.Id);

        // The two queries this table serves besides the primary-key lookup: bulk
        // revocation when a surface changes its identity provider, and the expiry purge.
        builder.HasIndex(x => new { x.WorkspaceKey, x.SurfaceKey });
        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.Property(x => x.TenantKey).HasColumnName("tenant_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasColumnName("workspace_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SurfaceKey).HasColumnName("surface_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Audience).HasColumnName("audience").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClaimsJson).HasColumnName("claims_json").IsRequired();
        builder.Property(x => x.AuthenticationMethod).HasColumnName("authentication_method").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AuthenticatedAtUtc).HasColumnName("authenticated_at_utc").IsRequired();
        builder.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(x => x.IdentityPluginId).HasColumnName("identity_plugin_id").HasMaxLength(200);
        builder.Property(x => x.IdentityVersion).HasColumnName("identity_version").HasMaxLength(80);
    }
}
