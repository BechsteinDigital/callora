using Callora.Core.Application.Surfaces;

namespace Callora.Surface.Rendering.Api.SurfaceContext;

/// <summary>
/// Re-checks, while a context socket is open, that the session behind it is still the one it was
/// accepted with.
/// <para>
/// A WebSocket lives for hours; the permission behind it does not. Someone signs out, a session
/// expires, an operator reassigns a surface's identity provider — ADR-017 §6.3 makes
/// <c>IdentityAssignedAtUtc</c> the invalidation boundary for older sessions. Without a re-check,
/// shared context would keep flowing into a tab that may no longer see it, and the longer the
/// connection the wider that window.
/// </para>
/// <para>
/// A CHANGED identity ends the connection too, not just a missing one. If the subject on the
/// cookie is no longer the subject the socket was accepted for, the anchors it holds are somebody
/// else's — dropping it and letting the client reconnect is the only correct answer.
/// </para>
/// </summary>
public sealed class SurfaceContextRevalidator
{
    /// <summary>
    /// How often the session behind an open socket is re-checked.
    /// <para>
    /// Thirty seconds is the compromise this buys: a shorter interval costs a session lookup per
    /// connection more often, a longer one widens the window in which a signed-out visitor still
    /// receives context. It is a backstop, not the primary defence — that is the projection, which
    /// already limits what any connection can receive at all.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly SurfaceSessionProbe? _probe;
    private readonly TimeProvider _time;

    /// <param name="probe">
    /// Re-reads the session behind the cookie. Null in a composition without the identity
    /// subsystem: there is then no session to lose, and watching is a no-op rather than a missing
    /// service — a host that composes less should not fail to start.
    /// <para>
    /// A delegate rather than the authenticator itself, so this stays testable without standing up
    /// a cookie codec, a session store and a surface store to answer one question.
    /// </para>
    /// </param>
    /// <param name="timeProvider">Injected in tests to run the interval without waiting for it.</param>
    public SurfaceContextRevalidator(SurfaceSessionProbe? probe, TimeProvider? timeProvider = null)
    {
        _probe = probe;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Cancels <paramref name="connection"/> as soon as its session stops being valid, or stops
    /// when the connection ends on its own.
    /// </summary>
    /// <param name="cookieValue">The cookie the socket was accepted with.</param>
    /// <param name="audience">Host the upgrade arrived on; a cookie is bound to it.</param>
    /// <param name="acceptedSubjectId">Who the socket was accepted for, or null for anonymous.</param>
    /// <param name="connection">Cancelled when the session no longer holds.</param>
    public async Task WatchAsync(
        string? cookieValue,
        string audience,
        string? acceptedSubjectId,
        CancellationTokenSource connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // An anonymous connection has no session to lose. It receives surface-wide values only,
        // and those are not tied to an identity that could be revoked.
        if (_probe is null || string.IsNullOrEmpty(cookieValue))
        {
            return;
        }

        try
        {
            while (!connection.IsCancellationRequested)
            {
                await Task.Delay(Interval, _time, connection.Token).ConfigureAwait(false);

                var caller = await _probe(cookieValue, audience, connection.Token).ConfigureAwait(false);

                if (caller?.Caller.Subject.SubjectId != acceptedSubjectId)
                {
                    await connection.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection ended first; nothing to invalidate.
        }
    }
}
