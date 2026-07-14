using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class PluginDataDocumentEntityTypeConfiguration : IEntityTypeConfiguration<PluginDataDocument>
{
    public void Configure(EntityTypeBuilder<PluginDataDocument> builder)
    {
        builder.ToTable("plugin_data_documents");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PluginId, x.WorkspaceKey, x.Collection, x.EntryKey }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Collection).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntryKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.JsonDocument).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
