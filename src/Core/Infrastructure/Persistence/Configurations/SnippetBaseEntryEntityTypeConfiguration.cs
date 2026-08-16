using Callora.Core.Domain.Snippets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class SnippetBaseEntryEntityTypeConfiguration : IEntityTypeConfiguration<SnippetBaseEntry>
{
    public void Configure(EntityTypeBuilder<SnippetBaseEntry> builder)
    {
        builder.ToTable("snippet_base");
        builder.HasKey(x => x.Id);

        // Ein Text je (Paket, Schlüssel, Locale). Das Präfix hält Pakete auseinander, dieser Index
        // hält ein Paket mit sich selbst auseinander.
        builder.HasIndex(x => new { x.PluginId, x.SnippetKey, x.Locale }).IsUnique();

        // Gelesen wird je Locale über alle Pakete — das ist die Abfrage des Renderpfads.
        builder.HasIndex(x => x.Locale);

        builder.Property(x => x.PluginId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SnippetKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(35).IsRequired();
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(60).IsRequired();
    }
}
