using Callora.Core.Domain.Snippets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class SnippetOverrideEntityTypeConfiguration : IEntityTypeConfiguration<SnippetOverride>
{
    public void Configure(EntityTypeBuilder<SnippetOverride> builder)
    {
        builder.ToTable("snippet_overrides");
        builder.HasKey(x => x.Id);

        // Ein Text je (Schlüssel, Locale, Geltungsbereich) — die Adresse aus ADR-024 §2 als
        // Datenbankzusage, damit zwei Wege zum selben Feld nicht zwei Zeilen erzeugen.
        builder.HasIndex(x => new { x.SnippetKey, x.Locale, x.Scope, x.ScopeKey }).IsUnique();

        builder.Property(x => x.SnippetKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(35).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ScopeKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
