using Callora.Core.Domain.Entitlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class MarketplaceEntitlementEventEntityTypeConfiguration
    : IEntityTypeConfiguration<MarketplaceEntitlementEventRecord>
{
    public void Configure(EntityTypeBuilder<MarketplaceEntitlementEventRecord> builder)
    {
        builder.ToTable("marketplace_entitlement_events");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.EventId).IsUnique();

        builder.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TenantKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(200);
        builder.Property(x => x.ProcessedAtUtc).IsRequired();
    }
}
