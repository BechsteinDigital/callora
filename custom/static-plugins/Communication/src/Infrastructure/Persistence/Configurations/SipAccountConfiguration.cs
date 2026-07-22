using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="SipAccount"/>; the connection value object is owned.</summary>
public sealed class SipAccountConfiguration : IEntityTypeConfiguration<SipAccount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SipAccount> builder)
    {
        builder.ToTable("sip_accounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaxConcurrentCalls).IsRequired();
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(500);

        builder.OwnsOne(x => x.Connection, connection =>
        {
            connection.Property(p => p.Host).HasColumnName("host").HasMaxLength(255).IsRequired();
            connection.Property(p => p.Port).HasColumnName("port").IsRequired();
            connection.Property(p => p.Transport).HasColumnName("transport").HasConversion<string>().HasMaxLength(10).IsRequired();
            connection.Property(p => p.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(10).IsRequired();
            // Registration expiry only applies to a registering connection; a trunk leaves it null.
            connection.Property(p => p.RegistrationExpirySeconds).HasColumnName("registration_expiry_seconds");
            // Polymorphic authentication persisted as one JSON column with a method discriminator —
            // an IP-authenticated trunk stores no credentials at all (see SipAuthenticationJsonConverter).
            connection.Property(p => p.Authentication)
                .HasColumnName("authentication")
                .HasConversion(new SipAuthenticationJsonConverter())
                .HasColumnType("text")
                .IsRequired();
        });

        builder.HasIndex(x => x.WorkspaceKey);

        // The composite workspace-FK from SipLine targets (WorkspaceKey, Id); EF materializes that
        // principal key as a unique alternate key (AK_sip_accounts_WorkspaceKey_Id) — the composite
        // unique constraint that lets a line only reference an account in its own workspace. No
        // separate unique index is declared here; it would duplicate that alternate key.
    }
}
