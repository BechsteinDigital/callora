using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class PluginInstallationEntityTypeConfiguration : IEntityTypeConfiguration<PluginInstallation>
{
    public void Configure(EntityTypeBuilder<PluginInstallation> builder)
    {
        builder.ToTable("plugin_installations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PluginId).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        // Die Spalte heißt weiter AssemblyPath: Was sich mit #307 ändert, ist der Inhalt (ein
        // Pfad relativ zur Plugin-Wurzel statt eines absoluten), nicht das Schema. Deshalb
        // braucht der Umbau keine Migration — und Bestand bleibt lesbar.
        builder.Property(x => x.StoredAssemblyPath).HasColumnName("AssemblyPath").HasMaxLength(2048).IsRequired();

        // Der aufgelöste Pfad gehört dem Prozess, nicht der Zeile. Würde EF ihn mitschreiben,
        // stünde nach dem ersten Speichern wieder ein absoluter Pfad in der Datenbank.
        builder.Ignore(x => x.AssemblyPath);
        builder.Property(x => x.EntryTypeName).HasMaxLength(800);
        builder.Property(x => x.State).HasConversion<int>().IsRequired();
        builder.Property(x => x.InstalledAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.ProvidedCapabilities).HasMaxLength(2000);
        builder.Property(x => x.RequiredCapabilities).HasMaxLength(2000);
        builder.Property(x => x.ConditionalCapabilities).HasMaxLength(2000);
    }
}
