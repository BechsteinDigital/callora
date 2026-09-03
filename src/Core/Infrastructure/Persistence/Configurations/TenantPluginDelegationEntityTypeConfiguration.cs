using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class TenantPluginDelegationEntityTypeConfiguration
    : IEntityTypeConfiguration<TenantPluginDelegation>
{
    public void Configure(EntityTypeBuilder<TenantPluginDelegation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenant_plugin_delegations");
        builder.HasKey(x => x.Id);

        // Eine Entscheidung je Mandant und Plugin. Zwei Zeilen wären zwei Antworten, und die
        // Auswertung entschiede still und immer gleich, welche gilt.
        builder.HasIndex(x => new { x.TenantKey, x.PluginId }).IsUnique();

        builder.Property(x => x.TenantKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.WorkspacesMayAssign).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
