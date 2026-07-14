using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class PluginInstallationEntityTypeConfiguration : IEntityTypeConfiguration<PluginInstallation>
{
    public void Configure(EntityTypeBuilder<PluginInstallation> builder)
    {
        builder.ToTable("plugin_installations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PluginId).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.AssemblyPath).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.EntryTypeName).HasMaxLength(800);
        builder.Property(x => x.State).HasConversion<int>().IsRequired();
        builder.Property(x => x.InstalledAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.ProvidedCapabilities).HasMaxLength(2000);
        builder.Property(x => x.RequiredCapabilities).HasMaxLength(2000);
    }
}
