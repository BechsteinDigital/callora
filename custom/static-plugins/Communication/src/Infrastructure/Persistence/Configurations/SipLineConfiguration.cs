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
        builder.HasIndex(x => x.AccountId);
    }
}
