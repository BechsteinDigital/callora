using Callora.Core.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceSectionLayoutDefinitionEntityTypeConfiguration
    : IEntityTypeConfiguration<WorkspaceSectionLayoutDefinition>
{
    public void Configure(EntityTypeBuilder<WorkspaceSectionLayoutDefinition> builder)
    {
        builder.ToTable("workspace_section_layout_definitions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.LayoutKey, x.PluginId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.PluginId, x.Version, x.IsActive });

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LayoutKey).HasColumnName("layout_key").HasMaxLength(180).IsRequired();
        builder.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(300).IsRequired();
        builder.Property(x => x.RegionsJson).HasColumnName("regions_json").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
