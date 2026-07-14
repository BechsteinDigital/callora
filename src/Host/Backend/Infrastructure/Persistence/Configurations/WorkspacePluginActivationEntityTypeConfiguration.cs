using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class WorkspacePluginActivationEntityTypeConfiguration : IEntityTypeConfiguration<WorkspacePluginActivation>
{
    public void Configure(EntityTypeBuilder<WorkspacePluginActivation> builder)
    {
        builder.ToTable("workspace_plugin_activations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantKey, x.WorkspaceKey, x.PluginId }).IsUnique();
        builder.HasIndex(x => new { x.TenantKey, x.PluginId });
        builder.HasIndex(x => x.PluginId);

        builder.Property(x => x.TenantKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
