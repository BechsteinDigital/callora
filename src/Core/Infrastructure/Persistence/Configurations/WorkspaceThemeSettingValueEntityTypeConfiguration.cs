using Callora.Core.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceThemeSettingValueEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceThemeSettingValue>
{
    public void Configure(EntityTypeBuilder<WorkspaceThemeSettingValue> builder)
    {
        builder.ToTable("workspace_theme_setting_values");
        builder.HasKey(x => x.Id);
        // The surface key is part of the identity: one row per level, with the
        // empty key standing for the workspace level.
        builder.HasIndex(x => new { x.WorkspaceKey, x.SurfaceKey, x.PluginId, x.SettingKey }).IsUnique();
        builder.HasIndex(x => new { x.PluginId, x.SettingKey });

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkspaceKey).HasColumnName("workspace_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SurfaceKey)
            .HasColumnName("surface_key")
            .HasMaxLength(120)
            .IsRequired()
            .HasDefaultValue(string.Empty);
        builder.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SettingKey).HasColumnName("setting_key").HasMaxLength(180).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnName("value_json").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
