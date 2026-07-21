using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Streaming;

/// <summary>
/// Binds a live call to the WebSocket media stream of an external consumer (Twilio-Media-
/// Streams-style). Metadata only — no audio is persisted. The <see cref="ConnectToken"/> is a
/// short-lived, single-use credential the host validates when the consumer opens the socket;
/// it is consumed by the <see cref="Activate"/> transition, so one token authorizes exactly
/// one connect.
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
        ConnectToken = connectToken;
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

    /// <summary>Short-lived, single-use credential the host validates on WS connect.</summary>
    public string ConnectToken { get; }

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
    /// still <see cref="MediaStreamSessionStatus.Pending"/> and within <paramref name="timeToLive"/>
    /// of creation.
    /// </summary>
    public bool CanActivate(DateTimeOffset now, TimeSpan timeToLive) =>
        Status == MediaStreamSessionStatus.Pending && now - CreatedAt <= timeToLive;

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
