using Callora.Core.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class BackendRevokedSessionEntityTypeConfiguration : IEntityTypeConfiguration<BackendRevokedSession>
{
    public void Configure(EntityTypeBuilder<BackendRevokedSession> builder)
    {
        builder.ToTable("backend_revoked_sessions");
        builder.HasKey(x => x.TokenId);

        builder.Property(x => x.TokenId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.RevokedAtUtc).IsRequired();

        // The purge job and the hot-path lookup both filter on expiry.
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
