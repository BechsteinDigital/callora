using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Surfaces.Events;
using Callora.Core.Application.Workspaces;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Turns the outcome of identity resolution into the caller for a request and the
/// cookie that should carry it (ADR-017 §8).
/// <para>
/// The rule that shapes everything here: a context exists for every visitor, but only
/// an authenticated one gets a server-side record. Guests stay in their signed cookie
/// so an anonymous page view never writes to the database; authenticated sessions are
/// stored so they can be revoked.
/// </para>
/// </summary>
public sealed class SurfaceSessionService
{
    private readonly ISurfaceSessionStore _sessions;
    private readonly ISurfaceSessionCookieCodec _codec;
    private readonly IBusinessEventBus _eventBus;
    private readonly SurfaceIdentityOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SurfaceSessionService> _logger;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="sessions">Server-side storage for authenticated sessions.</param>
    /// <param name="codec">Protects and reads the surface cookie.</param>
    /// <param name="eventBus">Bus the guest promotion event is published on.</param>
    /// <param name="options">Host bounds on guest and session lifetime.</param>
    /// <param name="timeProvider">Clock for issue and expiry decisions.</param>
    /// <param name="logger">Diagnostics.</param>
    public SurfaceSessionService(
        ISurfaceSessionStore sessions,
        ISurfaceSessionCookieCodec codec,
        IBusinessEventBus eventBus,
        SurfaceIdentityOptions options,
        TimeProvider timeProvider,
        ILogger<SurfaceSessionService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _sessions = sessions;
        _codec = codec;
        _eventBus = eventBus;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Establishes the caller for one request.
    /// </summary>
    /// <param name="surface">The resolved surface, carrying its identity assignment.</param>
    /// <param name="audience">Host the request arrived on.</param>
    /// <param name="cookieValue">Incoming surface cookie value, if any.</param>
    /// <param name="resolution">Outcome of identity resolution for this request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceSessionEstablishment> EstablishAsync(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        string? cookieValue,
        SurfaceIdentityResolution resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentNullException.ThrowIfNull(resolution);

        var existing = ReadEnvelope(cookieValue, surface, audience);

        if (resolution.Caller is { } authenticated)
        {
            return await PromoteAsync(surface, audience, existing, authenticated, cancellationToken)
                .ConfigureAwait(false);
        }

        if (resolution.IsClosed)
        {
            return ContinueWhileClosed(surface, audience, existing, resolution);
        }

        return await ContinueAsGuestAsync(surface, audience, existing, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the authenticated session behind a cookie and returns a fresh guest
    /// context. Rotating rather than merely deleting keeps the visitor usable — and
    /// keeps the next login from reusing a token an attacker may already know.
    /// </summary>
    /// <param name="surface">The resolved surface.</param>
    /// <param name="audience">Host the request arrived on.</param>
    /// <param name="cookieValue">Incoming surface cookie value, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceSessionEstablishment> EndSessionAsync(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        string? cookieValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        var existing = ReadEnvelope(cookieValue, surface, audience);
        if (existing is { Kind: SurfaceSessionEnvelopeKind.Authenticated } && Guid.TryParse(existing.Id, out var id))
        {
            await _sessions.RevokeAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return MintGuest(surface, audience, SurfaceIdentityResolutionStatus.Anonymous);
    }

    private SurfaceSessionEnvelope? ReadEnvelope(
        string? cookieValue,
        WorkspaceSurfaceSnapshot surface,
        string audience)
    {
        if (_codec.Unprotect(cookieValue) is not { } envelope)
        {
            return null;
        }

        // A cookie is host-bound, a surface is not, and two surfaces can share a host.
        // A scope mismatch is discarded rather than repaired.
        if (!envelope.MatchesScope(surface.TenantKey, surface.WorkspaceKey, surface.SurfaceKey, audience))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (envelope.Kind == SurfaceSessionEnvelopeKind.Guest &&
            envelope.IssuedAtUtc + _options.GuestContextLifetime <= now)
        {
            return null;
        }

        // A change of identity provider voids everything issued before it: if another
        // party now vouches for the surface's visitors, carrying trust over would be
        // inconsistent (ADR-017 §6.3). Guests are unaffected — they vouch for nothing.
        if (envelope.Kind == SurfaceSessionEnvelopeKind.Authenticated &&
            surface.IdentityAssignedAtUtc is { } assignedAt &&
            envelope.IssuedAtUtc < assignedAt)
        {
            return null;
        }

        return envelope;
    }

    private async Task<SurfaceSessionEstablishment> PromoteAsync(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        SurfaceSessionEnvelope? existing,
        AuthenticatedSurfaceCaller caller,
        CancellationToken cancellationToken)
    {
        if (existing is { Kind: SurfaceSessionEnvelopeKind.Authenticated } &&
            Guid.TryParse(existing.Id, out var existingId))
        {
            var stored = await _sessions.GetAsync(existingId, cancellationToken).ConfigureAwait(false);
            if (stored is not null && stored.Subject == caller.Subject &&
                stored.ExpiresAtUtc > _timeProvider.GetUtcNow())
            {
                await _sessions.TouchAsync(existingId, _timeProvider.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false);
                return new SurfaceSessionEstablishment(caller, SurfaceIdentityResolutionStatus.Authenticated);
            }

            // Same cookie, different or stale subject: the old session ends here rather
            // than lingering as a second valid credential for the same browser.
            await _sessions.RevokeAsync(existingId, cancellationToken).ConfigureAwait(false);
        }

        var session = await MintSessionAsync(surface, audience, caller, cancellationToken).ConfigureAwait(false);

        if (existing is { Kind: SurfaceSessionEnvelopeKind.Guest })
        {
            // The token rotates for session-fixation reasons, so the subject changes
            // with it. Only the owning plugin can move what hung off the old subject.
            await PublishPromotionAsync(
                    surface,
                    new SurfaceSubject(SurfaceIdentityIssuers.Guest, existing.Id),
                    caller.Subject,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var envelope = new SurfaceSessionEnvelope(
            SurfaceSessionEnvelope.CurrentVersion,
            SurfaceSessionEnvelopeKind.Authenticated,
            session.SessionId.ToString("N"),
            surface.TenantKey,
            surface.WorkspaceKey,
            surface.SurfaceKey,
            audience,
            session.IssuedAtUtc);

        return new SurfaceSessionEstablishment(
            caller,
            SurfaceIdentityResolutionStatus.Authenticated,
            _codec.Protect(envelope),
            session.ExpiresAtUtc);
    }

    private async Task<SurfaceSession> MintSessionAsync(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        AuthenticatedSurfaceCaller caller,
        CancellationToken cancellationToken)
    {
        var session = new SurfaceSession(
            Guid.NewGuid(),
            surface.TenantKey,
            surface.WorkspaceKey,
            surface.SurfaceKey,
            audience,
            caller.Subject,
            caller.Identity,
            _timeProvider.GetUtcNow(),
            caller.Identity.ExpiresAtUtc,
            surface.IdentityPluginId,
            surface.IdentityVersion);

        await _sessions.CreateAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    private async Task<SurfaceSessionEstablishment> ContinueAsGuestAsync(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        SurfaceSessionEnvelope? existing,
        CancellationToken cancellationToken)
    {
        if (existing is { Kind: SurfaceSessionEnvelopeKind.Authenticated } &&
            Guid.TryParse(existing.Id, out var staleId))
        {
            // The provider no longer recognises this visitor, so the session it
            // vouched for ends too — a stored session outliving its own identity
            // would keep answering for someone the provider has forgotten.
            await _sessions.RevokeAsync(staleId, cancellationToken).ConfigureAwait(false);
            existing = null;
        }

        if (existing is { Kind: SurfaceSessionEnvelopeKind.Guest })
        {
            return new SurfaceSessionEstablishment(
                new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, existing.Id)),
                SurfaceIdentityResolutionStatus.Anonymous);
        }

        return MintGuest(surface, audience, SurfaceIdentityResolutionStatus.Anonymous);
    }

    private SurfaceSessionEstablishment ContinueWhileClosed(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        SurfaceSessionEnvelope? existing,
        SurfaceIdentityResolution resolution)
    {
        // An existing session is left untouched: the provider is unreachable, not
        // gone, and revoking here would sign every visitor out over a transient
        // outage. It simply cannot be used while the surface is closed, so the
        // request continues under a throwaway guest without writing a cookie.
        if (existing is { Kind: SurfaceSessionEnvelopeKind.Authenticated })
        {
            _logger.LogWarning(
                "Surface {SurfaceKey} in workspace {WorkspaceKey} is closed for authenticated access ({Status}); an existing session is preserved but not honoured.",
                surface.SurfaceKey,
                surface.WorkspaceKey,
                resolution.Status);
            return new SurfaceSessionEstablishment(
                new GuestSurfaceCaller(SurfaceGuestSubjectFactory.Create()),
                resolution.Status,
                Detail: resolution.Detail);
        }

        if (existing is { Kind: SurfaceSessionEnvelopeKind.Guest })
        {
            return new SurfaceSessionEstablishment(
                new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, existing.Id)),
                resolution.Status,
                Detail: resolution.Detail);
        }

        return MintGuest(surface, audience, resolution.Status, resolution.Detail);
    }

    private SurfaceSessionEstablishment MintGuest(
        WorkspaceSurfaceSnapshot surface,
        string audience,
        SurfaceIdentityResolutionStatus status,
        string? detail = null)
    {
        var subject = SurfaceGuestSubjectFactory.Create();
        var issuedAt = _timeProvider.GetUtcNow();
        var envelope = new SurfaceSessionEnvelope(
            SurfaceSessionEnvelope.CurrentVersion,
            SurfaceSessionEnvelopeKind.Guest,
            subject.SubjectId,
            surface.TenantKey,
            surface.WorkspaceKey,
            surface.SurfaceKey,
            audience,
            issuedAt);

        return new SurfaceSessionEstablishment(
            new GuestSurfaceCaller(subject),
            status,
            _codec.Protect(envelope),
            issuedAt + _options.GuestContextLifetime,
            detail);
    }

    private async Task PublishPromotionAsync(
        WorkspaceSurfaceSnapshot surface,
        SurfaceSubject previous,
        SurfaceSubject current,
        CancellationToken cancellationToken)
    {
        try
        {
            await _eventBus
                .PublishAsync(
                    SurfaceCallerBusinessEvent.Promoted(
                        surface.WorkspaceKey, surface.SurfaceKey, previous, current),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A listener that fails must not block the login it is reacting to; the
            // visitor is authenticated either way. The cost is a plugin that misses a
            // cart migration, which is why this is logged as an error.
            _logger.LogError(
                ex,
                "Publishing the guest promotion for surface {SurfaceKey} in workspace {WorkspaceKey} failed.",
                surface.SurfaceKey,
                surface.WorkspaceKey);
        }
    }
}
