using Callora.Host.Backend.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class SystemConfigValueEntityTypeConfiguration : IEntityTypeConfiguration<SystemConfigValue>
{
    public void Configure(EntityTypeBuilder<SystemConfigValue> builder)
    {
        builder.ToTable("system_config_values");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PluginId, x.ConfigKey, x.Scope, x.ScopeKey }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ConfigKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ScopeKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ValueJson).IsRequired();
    }
}
