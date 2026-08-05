using System.Text;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// The <see cref="IHostSessionResumeService"/> a plugin actually receives: bound to the plugin the
/// host handed it to, so a ticket can only ever be redeemed by whoever issued it (ADR-018 §2.2).
/// </summary>
/// <remarks>
/// Two limits are enforced here rather than left to the caller, because both turn a reconnect
/// mechanism into something else when they are missing. An unbounded lifetime makes the token a
/// standing bearer credential, and an unbounded payload makes the ticket table a document store.
/// </remarks>
internal sealed class PluginSessionResumeService(
    ISessionResumeTicketStore store,
    TimeProvider timeProvider,
    CalloraHostingOptions options,
    string pluginId) : IHostSessionResumeService
{
    public async Task<HostSessionResumeTicket> IssueAsync(
        string sessionKind,
        string payload,
        TimeSpan lifetime,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKind);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        if (payloadBytes > options.SessionResumeMaxPayloadBytes)
        {
            // Refused rather than truncated: a payload the plugin cannot rebuild from is worse than
            // no ticket, because the failure would only show up on the reconnect.
            throw new ArgumentException(
                $"The resume payload is {payloadBytes} bytes, above the host limit of " +
                $"{options.SessionResumeMaxPayloadBytes}. A resume payload carries identity (which " +
                "session, which participant), not session state.",
                nameof(payload));
        }

        var effectiveLifetime = lifetime > options.SessionResumeMaxLifetime
            ? options.SessionResumeMaxLifetime
            : lifetime;

        var secret = SingleUseSecret.Create();
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt + effectiveLifetime;

        await store.CreateAsync(
            new SessionResumeTicketRecord
            {
                Id = Guid.NewGuid(),
                TokenHash = SingleUseSecret.Hash(secret),
                PluginId = pluginId,
                SessionKind = sessionKind,
                WorkspaceKey = workspaceKey ?? string.Empty,
                Payload = payload,
                IssuedAtUtc = issuedAt,
                ExpiresAtUtc = expiresAt,
            },
            cancellationToken).ConfigureAwait(false);

        return new HostSessionResumeTicket(secret, expiresAt);
    }

    public async Task<HostSessionResume?> RedeemAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var record = await store
            .ConsumeAsync(SingleUseSecret.Hash(token), pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        // An expired ticket is consumed anyway and then refused. Removing it costs nothing here and
        // saves the purge a row.
        if (record.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return null;
        }

        return new HostSessionResume(
            record.SessionKind,
            record.Payload,
            string.IsNullOrEmpty(record.WorkspaceKey) ? null : record.WorkspaceKey,
            record.IssuedAtUtc);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        await store
            .DeleteAsync(SingleUseSecret.Hash(token), pluginId, cancellationToken)
            .ConfigureAwait(false);
    }
}
