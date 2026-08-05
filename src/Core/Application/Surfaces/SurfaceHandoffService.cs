using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Moves an established identity to a surface on another host (ADR-017 §8.4).
/// <para>
/// Surfaces can have different hosts, so a cookie cannot follow the visitor across.
/// The alternative the platform refuses is a long-lived bearer token circulating
/// between every surface host. What travels instead is a one-time secret with a
/// lifetime measured in seconds, bound to one target host, exchanged there for a
/// session that belongs to that host alone.
/// </para>
/// </summary>
public sealed class SurfaceHandoffService(
    IWorkspaceSurfaceStore surfaces,
    ISurfaceHandoffTicketStore tickets,
    SurfaceIdentityOptions options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Issues a ticket for the target surface on behalf of an authenticated caller.
    /// </summary>
    /// <param name="source">Scope the calling cookie belongs to.</param>
    /// <param name="targetSurfaceKey">Surface the visitor is being sent to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceHandoffIssue> IssueAsync(
        SurfaceCallerContext source,
        string targetSurfaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSurfaceKey);

        if (source.Caller is not AuthenticatedSurfaceCaller caller)
        {
            return SurfaceHandoffIssue.Refuse(
                SurfaceHandoffStatus.NotAuthenticated,
                "Only an authenticated caller can be handed over.");
        }

        // Scoped to the caller's own workspace on purpose: an identity is bound to the
        // workspace it was issued in, so a surface elsewhere is simply not a target.
        var target = await surfaces
            .GetAsync(source.WorkspaceKey, targetSurfaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (target is null || !target.IsActive)
        {
            return SurfaceHandoffIssue.Refuse(
                SurfaceHandoffStatus.TargetUnavailable,
                $"Surface '{targetSurfaceKey}' is not available in workspace '{source.WorkspaceKey}'.");
        }

        if (ResolveAudience(target) is not { } audience)
        {
            return SurfaceHandoffIssue.Refuse(
                SurfaceHandoffStatus.TargetUnavailable,
                $"Surface '{targetSurfaceKey}' has no public host to hand over to.");
        }

        var now = timeProvider.GetUtcNow();
        var secret = SurfaceHandoffSecret.Create();
        var ticket = new SurfaceHandoffTicket(
            Guid.NewGuid(),
            source.TenantKey,
            source.WorkspaceKey,
            source.SurfaceKey,
            target.SurfaceKey,
            audience,
            caller.Subject,
            caller.Identity,
            now,
            // The ticket never outlives the identity it carries, and never lives long
            // in any case: it is a redirect's worth of time, not a session.
            Min(now + options.HandoffTicketLifetime, caller.Identity.ExpiresAtUtc));

        await tickets.CreateAsync(ticket, SurfaceHandoffSecret.Hash(secret), cancellationToken)
            .ConfigureAwait(false);

        return new SurfaceHandoffIssue(
            SurfaceHandoffStatus.Ok, secret, audience, target.SurfaceKey, ticket.ExpiresAtUtc);
    }

    /// <summary>
    /// Redeems a ticket at the target surface. Consumes it whether or not the
    /// remaining checks pass, so a rejected presentation cannot be retried.
    /// </summary>
    /// <param name="secret">The secret presented by the visitor.</param>
    /// <param name="audience">Host the redemption request arrived on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceHandoffRedemption> RedeemAsync(
        string? secret,
        string audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        if (string.IsNullOrWhiteSpace(secret))
        {
            return SurfaceHandoffRedemption.Refuse(SurfaceHandoffStatus.TicketInvalid, "No ticket presented.");
        }

        var ticket = await tickets
            .ConsumeAsync(SurfaceHandoffSecret.Hash(secret), cancellationToken)
            .ConfigureAwait(false);
        if (ticket is null || ticket.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return SurfaceHandoffRedemption.Refuse(
                SurfaceHandoffStatus.TicketInvalid, "Ticket is unknown, already used, or expired.");
        }

        if (!string.Equals(ticket.TargetAudience, audience, StringComparison.OrdinalIgnoreCase))
        {
            return SurfaceHandoffRedemption.Refuse(
                SurfaceHandoffStatus.AudienceMismatch,
                $"Ticket was minted for '{ticket.TargetAudience}' and presented on '{audience}'.");
        }

        var target = await surfaces
            .GetAsync(ticket.WorkspaceKey, ticket.TargetSurfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (target is null || !target.IsActive)
        {
            return SurfaceHandoffRedemption.Refuse(
                SurfaceHandoffStatus.TargetUnavailable, "Target surface is gone or inactive.");
        }

        // The same rule as for a session cookie: a provider assigned after the ticket
        // was minted did not vouch for the identity it carries (ADR-017 §6.3).
        if (target.IdentityAssignedAtUtc is { } assignedAt && ticket.IssuedAtUtc < assignedAt)
        {
            return SurfaceHandoffRedemption.Refuse(
                SurfaceHandoffStatus.TicketInvalid, "Ticket predates the target surface's identity provider.");
        }

        return new SurfaceHandoffRedemption(
            SurfaceHandoffStatus.Ok,
            target with { TenantKey = ticket.TenantKey },
            new AuthenticatedSurfaceCaller(ticket.Subject, ticket.Identity));
    }

    private static string? ResolveAudience(WorkspaceSurfaceSnapshot surface)
    {
        if (!string.IsNullOrWhiteSpace(surface.PublicHost))
        {
            return surface.PublicHost.Trim();
        }

        return Uri.TryCreate(surface.PublicBaseUrl, UriKind.Absolute, out var parsed) ? parsed.Host : null;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;
}
