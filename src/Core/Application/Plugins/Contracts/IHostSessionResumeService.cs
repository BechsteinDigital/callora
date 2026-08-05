using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Lets a plugin promise a client it can pick a real-time session back up after the connection
/// drops (ADR-018 §2.2), whether the drop was a tunnel, a lost WiFi or a host restart.
/// </summary>
/// <remarks>
/// <para>
/// <b>A session is not stored, a promise is.</b> Live sessions hold sockets, SDK peers and
/// negotiated media state, none of which survive serialization or a process boundary. What the host
/// keeps is a token, a deadline, the owning plugin and an <i>opaque</i> payload it never interprets.
/// On redemption the plugin gets its payload back and rebuilds the session, which is what a
/// reconnecting client does anyway: drop the peer connection and let the server offer again.
/// </para>
/// <para>
/// The host owns the token, its lifetime and the store; the plugin owns what the payload means.
/// Tickets are bound to the plugin that issued them, so one plugin can never redeem another's.
/// </para>
/// <para>
/// Redemption is single use. A client that reconnects and wants to stay resumable asks for a fresh
/// ticket, which keeps an intercepted token worthless the moment it has been spent.
/// </para>
/// </remarks>
[CalloraExtensible("Host service — resolve from IHostPluginContext.Services to make a real-time session resumable across reconnects and restarts (ADR-018 §2.2)")]
public interface IHostSessionResumeService
{
    /// <summary>
    /// Issues a resume ticket for a session that is live right now. Hand the returned token to the
    /// client while the connection is healthy: a client that only learns it when the socket dies has
    /// already missed its chance.
    /// </summary>
    /// <param name="sessionKind">
    /// The plugin's own name for what kind of session this is, echoed back on redemption so a plugin
    /// serving several session types can tell them apart.
    /// </param>
    /// <param name="payload">
    /// Whatever the plugin needs to rebuild the session, opaque to the host. Keep it to identity
    /// (which room, which participant, which role) rather than state; anything larger than the host's
    /// payload limit is rejected.
    /// </param>
    /// <param name="lifetime">
    /// How long the promise holds. Clamped to the host's maximum: a reconnect window measured in days
    /// is not a reconnect window, it is a bearer credential.
    /// </param>
    /// <param name="workspaceKey">Workspace the session belongs to, when it has one. Kept for audit.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<HostSessionResumeTicket> IssueAsync(
        string sessionKind,
        string payload,
        TimeSpan lifetime,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a token once. Returns <see langword="null"/> when it is unknown, already spent,
    /// expired, or belongs to another plugin — all four are the same answer to the caller on purpose,
    /// so a probe learns nothing from which one it hit.
    /// </summary>
    Task<HostSessionResume?> RedeemAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a ticket that will not be used. Call it when the client leaves deliberately: a hang-up
    /// and a dropped connection look identical to the server, and only the former should give up the
    /// right to come back.
    /// </summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
