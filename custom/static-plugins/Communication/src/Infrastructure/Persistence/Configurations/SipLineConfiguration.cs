using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Domain.Lines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="SipLine"/>.</summary>
public sealed class SipLineConfiguration : IEntityTypeConfiguration<SipLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SipLine> builder)
    {
        builder.ToTable("sip_lines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.AccountId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SipUri).HasMaxLength(255).IsRequired();
        builder.Property(x => x.PrimaryNumber).HasMaxLength(64);
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.InboundRoutingTarget).HasMaxLength(200);

        builder.HasIndex(x => x.WorkspaceKey);

        // Composite workspace-FK: a line's (WorkspaceKey, AccountId) must match an existing account's
        // (WorkspaceKey, Id). This makes a cross-workspace line→account reference structurally
        // impossible at the database, not just discouraged in the stores. No navigation — the
        // aggregates stay decoupled in the domain; this is a persistence-integrity constraint only.
        // Restrict, so an account cannot be deleted while it still has lines (the purge deletes
        // lines first). EF creates the covering index on (WorkspaceKey, AccountId).
        builder.HasOne<SipAccount>()
            .WithMany()
            .HasPrincipalKey(account => new { account.WorkspaceKey, account.Id })
            .HasForeignKey(line => new { line.WorkspaceKey, line.AccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
