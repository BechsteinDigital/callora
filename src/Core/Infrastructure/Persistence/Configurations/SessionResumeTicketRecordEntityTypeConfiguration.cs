using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class SessionResumeTicketRecordEntityTypeConfiguration
    : IEntityTypeConfiguration<SessionResumeTicketRecord>
{
    public void Configure(EntityTypeBuilder<SessionResumeTicketRecord> builder)
    {
        builder.ToTable("session_resume_tickets");
        builder.HasKey(x => x.Id);

        // Redemption looks the ticket up by hash and owner together, and the uniqueness on the hash
        // is what makes delete-and-return single-use rather than best effort.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SessionKind).HasColumnName("session_kind").HasMaxLength(120).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasColumnName("workspace_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
    }
}
