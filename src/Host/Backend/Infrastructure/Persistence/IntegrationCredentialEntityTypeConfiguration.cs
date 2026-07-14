using Callora.Host.Backend.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class IntegrationCredentialEntityTypeConfiguration : IEntityTypeConfiguration<IntegrationCredential>
{
    public void Configure(EntityTypeBuilder<IntegrationCredential> builder)
    {
        builder.ToTable("integration_credentials");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.KeyHash).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.KeyPrefix).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(40).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.RevokedAtUtc);
    }
}
