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
            connection.Property(p => p.AuthUsername).HasColumnName("auth_username").HasMaxLength(200).IsRequired();
            connection.Property(p => p.AuthId).HasColumnName("auth_id").HasMaxLength(200);
            connection.Property(p => p.PasswordSecretRef).HasColumnName("password_secret_ref").HasMaxLength(500).IsRequired();
            connection.Property(p => p.RegistrationExpirySeconds).HasColumnName("registration_expiry_seconds").IsRequired();
        });

        builder.HasIndex(x => x.WorkspaceKey);
    }
}
