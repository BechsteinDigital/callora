using System.Text.Json;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="SipAccount"/>; the connection value object is owned.</summary>
public sealed class SipAccountConfiguration : IEntityTypeConfiguration<SipAccount>
{
    private const char InboundNumbersSeparator = '\n';

    /// <summary>
    /// Persists the DID whitelist as one newline-joined column. Numbers never contain a newline
    /// (they are validated/trimmed by <see cref="SipConnection"/>), so the join is unambiguous.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<string>, string?> InboundNumbersConverter = new(
        list => list.Count == 0 ? null : string.Join(InboundNumbersSeparator, list),
        stored => Split(stored));

    /// <summary>
    /// EF needs an explicit comparer for a collection property, otherwise change tracking treats the
    /// reference as immutable and misses edits (and warns at model build).
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<string>> InboundNumbersComparer = new(
        (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        list => list.ToList());

    private static readonly JsonSerializerOptions CallQuotasJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Persists the line shares as one JSON column. They are read and written as a whole and never
    /// queried by origin, so a table of their own would buy nothing but a join.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<CallQuota>, string?> CallQuotasConverter = new(
        list => list.Count == 0 ? null : JsonSerializer.Serialize(list, CallQuotasJson),
        stored => ReadQuotas(stored));

    /// <summary>Same reason as the DID whitelist: without it EF never notices an edited collection.</summary>
    private static readonly ValueComparer<IReadOnlyList<CallQuota>> CallQuotasComparer = new(
        (left, right) => (left ?? new List<CallQuota>()).SequenceEqual(right ?? new List<CallQuota>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        list => list.ToList());

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SipAccount> builder)
    {
        builder.ToTable("sip_accounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaxConcurrentCalls).IsRequired();
        var callQuotas = builder.Property(x => x.CallQuotas)
            .HasColumnName("call_quotas")
            .HasColumnType("text")
            .HasConversion(CallQuotasConverter)
            .IsRequired(false); // an undivided trunk stores NULL, not "[]"
        callQuotas.Metadata.SetValueComparer(CallQuotasComparer);
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        // Matches SipStatusError.MaxLength: the domain truncates before this ever binds.
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.LastRegisteredAt);

        builder.OwnsOne(x => x.Connection, connection =>
        {
            connection.Property(p => p.Host).HasColumnName("host").HasMaxLength(255).IsRequired();
            connection.Property(p => p.Port).HasColumnName("port").IsRequired();
            connection.Property(p => p.Transport).HasColumnName("transport").HasConversion<string>().HasMaxLength(10).IsRequired();
            connection.Property(p => p.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(10).IsRequired();
            // Registration expiry only applies to a registering connection; the registration-less
            // IP-authenticated trunk leaves it null.
            connection.Property(p => p.RegistrationExpirySeconds).HasColumnName("registration_expiry_seconds");
            // Trunk inbound behaviour: an optional signalling proxy and a DID whitelist. The whitelist
            // is one newline-joined column (converter) with an explicit comparer for change tracking.
            connection.Property(p => p.OutboundProxy).HasColumnName("outbound_proxy").HasMaxLength(255);
            var inboundNumbers = connection.Property(p => p.InboundNumbers)
                .HasColumnName("inbound_numbers")
                .HasColumnType("text")
                .HasConversion(InboundNumbersConverter)
                .IsRequired(false); // empty whitelist stores NULL, not an empty string
            inboundNumbers.Metadata.SetValueComparer(InboundNumbersComparer);
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

    private static IReadOnlyList<CallQuota> ReadQuotas(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? []
            : JsonSerializer.Deserialize<List<CallQuota>>(stored, CallQuotasJson) ?? [];

    private static IReadOnlyList<string> Split(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? []
            : stored.Split(InboundNumbersSeparator, StringSplitOptions.RemoveEmptyEntries);
}
