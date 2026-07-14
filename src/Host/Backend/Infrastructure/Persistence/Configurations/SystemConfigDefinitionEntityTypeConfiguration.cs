using Callora.Host.Backend.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class SystemConfigDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<SystemConfigDefinition>
{
    public void Configure(EntityTypeBuilder<SystemConfigDefinition> builder)
    {
        builder.ToTable("system_config_definitions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PluginId, x.ConfigKey }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ConfigKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(240).IsRequired();
        builder.Property(x => x.FieldType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.GroupName).HasMaxLength(120);
    }
}
