using Callora.Host.Backend.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class WorkspaceThemeSettingDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceThemeSettingDefinition>
{
    public void Configure(EntityTypeBuilder<WorkspaceThemeSettingDefinition> builder)
    {
        builder.ToTable("workspace_theme_setting_definitions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SettingKey, x.PluginId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.PluginId, x.Version, x.IsActive });

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SettingKey).HasColumnName("setting_key").HasMaxLength(180).IsRequired();
        builder.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(300).IsRequired();
        builder.Property(x => x.FieldType).HasColumnName("field_type").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.DefaultValueJson).HasColumnName("default_value_json");
        builder.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.GroupName).HasColumnName("group_name").HasMaxLength(180);
        builder.Property(x => x.OptionsJson).HasColumnName("options_json");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
