using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// In-memory, single-process store that both mints WebRTC signalling connect-tokens
/// (<see cref="Mint"/>) and serves as the concrete implementation of
/// <see cref="IWebRtcSignalingTokenStore"/> and <see cref="IWebRtcSignalingSessionResolver"/>.
/// </summary>
/// <remarks>
/// Each token is a 32-byte cryptographically-random hex string. Tokens are single-use:
/// <see cref="IWebRtcSignalingTokenStore.TryConsumeAsync"/> marks a token consumed (via
/// an interlocked flag) without removing the entry; <see cref="IWebRtcSignalingSessionResolver.ResolveAsync"/>
/// removes it in a second step. A race between two concurrent connects using the same token is
/// resolved by the interlocked compare — only one wins; the other gets <see langword="null"/>.
/// An expired token is also denied fail-closed. This is the same single-use/TTL semantic
/// as the media stream path's compare-and-swap, but kept fully in-process because
/// <see cref="WebRtcSignalingSession"/> holds runtime objects (<see cref="CalloraVoipSdk.WebRtc.IWebRtcClient"/>,
/// <see cref="Infrastructure.Sdk.WebRtcVoiceChannel"/>) that cannot be serialized to a database.
/// </remarks>
internal sealed class WebRtcSignalingSessionStore : IWebRtcSignalingTokenStore, IWebRtcSignalingSessionResolver
{
    private readonly ConcurrentDictionary<string, WebRtcSignalingSessionStoreEntry> _entries =
        new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <param name="timeProvider">Used to stamp entries at mint time and evaluate TTL at consume time.</param>
    public WebRtcSignalingSessionStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Stores <paramref name="session"/> and returns an opaque, cryptographically-random connect-token.
    /// </summary>
    public string Mint(WebRtcSignalingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _entries[token] = new WebRtcSignalingSessionStoreEntry(session, _timeProvider.GetUtcNow());
        return token;
    }

    /// <inheritdoc />
    public ValueTask<string?> TryConsumeAsync(
        string connectToken,
        DateTimeOffset now,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(connectToken, out var entry))
        {
            return ValueTask.FromResult<string?>(null);
        }

        // Fail-closed on expiry even before the atomic consume attempt.
        if (now - entry.CreatedAt > timeToLive)
        {
            _entries.TryRemove(connectToken, out _);
            return ValueTask.FromResult<string?>(null);
        }

        // Atomic single-use gate: only the first thread to flip 0→1 gets the subject.
        var wasConsumed = Interlocked.Exchange(ref entry.Consumed, 1);
        if (wasConsumed != 0)
        {
            return ValueTask.FromResult<string?>(null);
        }

        // Subject == token; the resolver uses it as the lookup key.
        return ValueTask.FromResult<string?>(connectToken);
    }

    /// <inheritdoc />
    public ValueTask<WebRtcSignalingSession?> ResolveAsync(
        string? subject,
        CancellationToken cancellationToken = default)
    {
        if (subject is null)
        {
            return ValueTask.FromResult<WebRtcSignalingSession?>(null);
        }

        _entries.TryRemove(subject, out var entry);
        return ValueTask.FromResult(entry?.Session);
    }
}
