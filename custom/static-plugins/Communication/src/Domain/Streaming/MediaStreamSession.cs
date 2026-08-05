using System.Security.Cryptography;
using System.Text;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Streaming;

/// <summary>
/// Binds a live call to the WebSocket media stream of an external consumer (Twilio-Media-
/// Streams-style). Metadata only — no audio is persisted.
/// <para>
/// The connect token is a short-lived, single-use credential the host validates when the
/// consumer opens the socket; it is consumed by the <see cref="Activate"/> transition, so one
/// token authorizes exactly one connect. Only its <see cref="ConnectTokenHash"/> is kept
/// (#108): the row is a lookup key, not a copy of a live credential, so a leaked database
/// row hands out no working ticket.
/// </para>
/// </summary>
public sealed class MediaStreamSession
{
    /// <summary>Creates a pending session bound to a call, minted with its connect token.</summary>
    public MediaStreamSession(
        string id,
        string callId,
        string workspaceKey,
        string consumerRef,
        string connectToken,
        AudioFormat format,
        MediaStreamDirection direction,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectToken);
        ArgumentNullException.ThrowIfNull(format);

        Id = id;
        CallId = callId;
        WorkspaceKey = workspaceKey;
        ConsumerRef = consumerRef;
        ConnectTokenHash = HashToken(connectToken);
        Format = format;
        Direction = direction;
        CreatedAt = createdAt;
        Status = MediaStreamSessionStatus.Pending;
    }

#pragma warning disable CS8618 // Materialisierungs-Seam: EF setzt die Properties (inkl. des OwnsOne-VO) nach dem Aufruf.
    private MediaStreamSession()
    {
    }
#pragma warning restore CS8618

    /// <summary>Stable session identifier.</summary>
    public string Id { get; }

    /// <summary>The call this stream is bound to.</summary>
    public string CallId { get; }

    /// <summary>Owning workspace.</summary>
    public string WorkspaceKey { get; }

    /// <summary>The external consumer this stream serves (for example <c>ai-agent</c>).</summary>
    public string ConsumerRef { get; }

    /// <summary>
    /// SHA-256 of the connect token, hex-encoded. The plaintext exists only in the
    /// response that mints the session; it is never stored.
    /// </summary>
    public string ConnectTokenHash { get; }

    /// <summary>
    /// One-way, deterministic hash of a connect token — deterministic so the store can
    /// look a presented token up, one-way so the stored value is not a credential. No
    /// salt: the token is high-entropy already, and a per-row salt would make lookup
    /// impossible.
    /// </summary>
    public static string HashToken(string connectToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectToken);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(connectToken.Trim())));
    }

    /// <summary>Audio frame format negotiated for this stream.</summary>
    public AudioFormat Format { get; private set; }

    /// <summary>Audio flow direction relative to the consumer.</summary>
    public MediaStreamDirection Direction { get; }

    /// <summary>Current lifecycle status.</summary>
    public MediaStreamSessionStatus Status { get; private set; }

    /// <summary>When the session (and its connect token) was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When the consumer connected, once activated.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>When the stream ended, once closed.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Whether the connect token may still be redeemed at <paramref name="now"/>: the session is
    /// still <see cref="MediaStreamSessionStatus.Pending"/> and its creation lies within
    /// <paramref name="timeToLive"/> — and in the past. A future <see cref="CreatedAt"/> would
    /// otherwise satisfy a bare lower-bound check forever (#108).
    /// </summary>
    public bool CanActivate(DateTimeOffset now, TimeSpan timeToLive) =>
        Status == MediaStreamSessionStatus.Pending &&
        CreatedAt <= now &&
        now - CreatedAt <= timeToLive;

    /// <summary>
    /// Whether the session may be purged at <paramref name="now"/>: it is closed, or its
    /// ticket has been unusable for longer than <paramref name="retention"/>. Spent and
    /// expired tickets must not accumulate (#108).
    /// </summary>
    public bool CanPurge(DateTimeOffset now, TimeSpan retention) =>
        (EndedAt ?? CreatedAt) + retention <= now;

    /// <summary>
    /// Consumes the connect token and marks the stream live. Single-use: only a
    /// <see cref="MediaStreamSessionStatus.Pending"/> session can activate, so a token
    /// authorizes exactly one connect.
    /// </summary>
    public void Activate(DateTimeOffset now)
    {
        if (Status != MediaStreamSessionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Media stream session '{Id}' cannot be activated from status {Status}.");
        }

        Status = MediaStreamSessionStatus.Active;
        StartedAt = now;
    }

    /// <summary>Closes the stream. Idempotent once closed.</summary>
    public void Close(DateTimeOffset now)
    {
        if (Status == MediaStreamSessionStatus.Closed)
        {
            return;
        }

        Status = MediaStreamSessionStatus.Closed;
        EndedAt = now;
    }
}
