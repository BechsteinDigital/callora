using Callora.Host.Backend.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class WorkspaceTemplateDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceTemplateDefinition>
{
    public void Configure(EntityTypeBuilder<WorkspaceTemplateDefinition> builder)
    {
        builder.ToTable("workspace_template_definitions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TemplateKey, x.Surface, x.PluginId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.Surface, x.IsActive });
        builder.HasIndex(x => x.TemplateKey);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TemplateKey).HasColumnName("template_key").HasMaxLength(180).IsRequired();
        builder.Property(x => x.Surface).HasColumnName("surface").HasMaxLength(40).IsRequired();
        builder.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.TemplatePath).HasColumnName("template_path").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ParentTemplateKey).HasColumnName("parent_template_key").HasMaxLength(180);
        builder.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(40).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
