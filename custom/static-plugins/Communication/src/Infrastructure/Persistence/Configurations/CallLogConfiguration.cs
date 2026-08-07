using System.Text.Json;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="CallLog"/> (call-history metadata).</summary>
public sealed class CallLogConfiguration : IEntityTypeConfiguration<CallLog>
{
    private static readonly JsonSerializerOptions JourneyJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Persists the call's steps as one JSON column. They are written once when the call ends and read
    /// as a whole; a table of their own would buy nothing but a join and a second lifetime to manage.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<CallJourneyStep>, string?> JourneyConverter = new(
        steps => steps.Count == 0 ? null : JsonSerializer.Serialize(steps, JourneyJson),
        stored => ReadJourney(stored));

    /// <summary>Without an explicit comparer EF treats the collection as immutable and never writes it.</summary>
    private static readonly ValueComparer<IReadOnlyList<CallJourneyStep>> JourneyComparer = new(
        (left, right) => (left ?? new List<CallJourneyStep>()).SequenceEqual(right ?? new List<CallJourneyStep>()),
        steps => steps.Aggregate(0, (hash, step) => HashCode.Combine(hash, step.GetHashCode())),
        steps => steps.ToList());

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
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RemoteParty).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LocalIdentity).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HandledBy).HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.DurationSeconds).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DisconnectCause).HasMaxLength(200);

        var journey = builder.Property(x => x.Journey)
            .HasColumnName("journey")
            .HasColumnType("text")
            .HasConversion(JourneyConverter)
            .IsRequired(false); // a call nobody recorded anything for stores NULL, not "[]"
        journey.Metadata.SetValueComparer(JourneyComparer);

        // Optimistic concurrency over PostgreSQL's xmin system column: a second writer for the
        // same call loses with DbUpdateConcurrencyException instead of silently overwriting a
        // finalized record (#113). A system column, so it costs no schema and no migration.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(x => new { x.WorkspaceKey, x.StartedAt });
    }

    private static IReadOnlyList<CallJourneyStep> ReadJourney(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? []
            : JsonSerializer.Deserialize<List<CallJourneyStep>>(stored, JourneyJson) ?? [];
}
