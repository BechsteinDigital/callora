using Callora.Core.Domain.Entitlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class PluginEntitlementEntityTypeConfiguration : IEntityTypeConfiguration<PluginEntitlement>
{
    public void Configure(EntityTypeBuilder<PluginEntitlement> builder)
    {
        builder.ToTable("plugin_entitlements");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PluginId, x.TenantKey, x.WorkspaceKey }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TenantKey).HasMaxLength(120);
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120);
        builder.Property(x => x.Source).HasMaxLength(60).IsRequired();
    }
}
