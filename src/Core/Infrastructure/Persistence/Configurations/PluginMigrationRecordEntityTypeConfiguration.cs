using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class PluginMigrationRecordEntityTypeConfiguration : IEntityTypeConfiguration<PluginMigrationRecord>
{
    public void Configure(EntityTypeBuilder<PluginMigrationRecord> builder)
    {
        builder.ToTable("plugin_migrations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PluginId, x.Version }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
    }
}
