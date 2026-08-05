using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="CallLog"/> (call-history metadata).</summary>
public sealed class CallLogConfiguration : IEntityTypeConfiguration<CallLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CallLog> builder)
    {
        builder.ToTable("call_logs");
        builder.HasKey(x => x.RecordId);

        // The provider's call id is unique within its channel, not globally (#113). Scoping the
        // uniqueness to workspace and channel is what lets two channels report the same id.
        // A record without a channel (AccountId null) is outside this constraint, because
        // PostgreSQL treats nulls as distinct; no production path writes one.
        builder.Property(x => x.Id).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.WorkspaceKey, x.AccountId, x.Id }).IsUnique();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.AccountId).HasMaxLength(64);
        builder.Property(x => x.LineId).HasMaxLength(64);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RemoteParty).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LocalIdentity).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HandledBy).HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.DurationSeconds).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DisconnectCause).HasMaxLength(200);

        // Optimistic concurrency over PostgreSQL's xmin system column: a second writer for the
        // same call loses with DbUpdateConcurrencyException instead of silently overwriting a
        // finalized record (#113). A system column, so it costs no schema and no migration.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(x => new { x.WorkspaceKey, x.StartedAt });
    }
}
