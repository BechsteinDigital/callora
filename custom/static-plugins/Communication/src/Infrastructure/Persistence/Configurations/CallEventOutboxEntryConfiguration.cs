using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the call-event outbox (#113).</summary>
public sealed class CallEventOutboxEntryConfiguration : IEntityTypeConfiguration<CallEventOutboxEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CallEventOutboxEntry> builder)
    {
        builder.ToTable("call_event_outbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.DeliveredAt);
        builder.Property(x => x.LastError).HasMaxLength(500);

        // The drain query filters on undelivered-and-due and orders by occurrence, so both
        // columns belong in the index that serves it.
        builder.HasIndex(x => new { x.DeliveredAt, x.NextAttemptAt });
        builder.HasIndex(x => x.OccurredAt);
    }
}
