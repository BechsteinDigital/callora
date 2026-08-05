using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Turns a surface cookie into a caller on seams that have no surface route to
/// resolve from: WebSocket upgrades and, later, the surface API (ADR-017 §9). The
/// render path resolves the surface from host and path first and then checks the
/// cookie against it. Here it is the other way round: the envelope names its own
/// scope, and the host verifies that scope is still real and still trusted.
/// <para>
/// It never mints anything. A request that arrives without a usable cookie has no
/// caller, which is a normal outcome on these seams: they are anonymous at the
/// platform layer and each consumer decides what to require.
/// </para>
/// </summary>
public sealed class SurfaceSessionAuthenticator(
    ISurfaceSessionCookieCodec codec,
    ISurfaceSessionStore sessions,
    IWorkspaceSurfaceStore surfaces,
    SurfaceIdentityOptions options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Resolves the caller a cookie stands for, or null when it is absent, tampered
    /// with, expired, bound to another host, or predates the surface's current
    /// identity provider.
    /// </summary>
    /// <param name="cookieValue">Incoming surface cookie value.</param>
    /// <param name="audience">Host the request arrived on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceCallerContext?> AuthenticateAsync(
        string? cookieValue,
        string audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        if (codec.Unprotect(cookieValue) is not { } envelope)
        {
            return null;
        }

        var surface = await surfaces
            .GetAsync(envelope.WorkspaceKey, envelope.SurfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (surface is null || !surface.IsActive)
        {
            return null;
        }

        // The envelope's own tenant is checked against the session record below. Here
        // the surface only supplies the provider generation, so the tenant it reports
        // (which not every projection fills) is deliberately not the authority.
        if (!SurfaceSessionEnvelopeValidator.IsUsable(
                envelope,
                envelope.TenantKey,
                surface.WorkspaceKey,
                surface.SurfaceKey,
                audience,
                surface.IdentityAssignedAtUtc,
                timeProvider.GetUtcNow(),
                options.GuestContextLifetime))
        {
            return null;
        }

        var caller = envelope.Kind == SurfaceSessionEnvelopeKind.Guest
            ? new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, envelope.Id))
            : await AuthenticateSessionAsync(envelope, audience, cancellationToken).ConfigureAwait(false);

        return caller is null
            ? null
            : new SurfaceCallerContext(
                caller, envelope.TenantKey, envelope.WorkspaceKey, envelope.SurfaceKey);
    }

    private async Task<SurfaceCaller?> AuthenticateSessionAsync(
        SurfaceSessionEnvelope envelope,
        string audience,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(envelope.Id, out var sessionId))
        {
            return null;
        }

        var session = await sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return null;
        }

        // The stored session is the authority on its own scope. A cookie whose
        // envelope disagrees with the row it points at is not repaired, it is refused.
        if (!string.Equals(session.TenantKey, envelope.TenantKey, StringComparison.Ordinal) ||
            !string.Equals(session.WorkspaceKey, envelope.WorkspaceKey, StringComparison.Ordinal) ||
            !string.Equals(session.SurfaceKey, envelope.SurfaceKey, StringComparison.Ordinal) ||
            !string.Equals(session.Audience, audience, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AuthenticatedSurfaceCaller(session.Subject, session.Identity);
    }
}
