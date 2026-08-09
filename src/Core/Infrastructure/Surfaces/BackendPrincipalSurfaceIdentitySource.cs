using System.Security.Claims;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Derives a surface identity from the backend principal already on the request
/// (ADR-017 §7). Internal users — an agent at their desktop, a doctor at a practice
/// workstation — are platform users; making them authenticate a second time through
/// a plugin would be absurd.
/// <para>
/// On an <see cref="SurfaceAuthentication.Administration"/> node it also carries the operator's
/// RBAC permissions as surface claims (ADR-023) — and nowhere else. That restriction is the
/// whole point: ADR-017 §7 forbade it while this source applied to any surface that merely
/// lacked an identity plugin, where it would have leaked admin rights onto a public website.
/// </para>
/// </summary>
public sealed class BackendPrincipalSurfaceIdentitySource(
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
    : ISurfaceHostIdentitySource
{
    /// <summary>Claim carrying the workspace a host-derived caller belongs to.</summary>
    public const string WorkspaceClaim = "host.workspace-key";

    /// <inheritdoc />
    public ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        SurfaceAuthentication authentication,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (httpContextAccessor.HttpContext?.User is not { Identity.IsAuthenticated: true } principal)
        {
            return ValueTask.FromResult(HostSurfaceIdentityResult.Anonymous);
        }

        if (ResolveSubjectId(principal) is not { } subjectId)
        {
            return ValueTask.FromResult(HostSurfaceIdentityResult.Anonymous);
        }

        var now = timeProvider.GetUtcNow();
        var boundWorkspace = principal.FindFirstValue(BackendClaimTypes.WorkspaceKey);
        var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(boundWorkspace))
        {
            claims[WorkspaceClaim] = [boundWorkspace];
        }

        if (authentication == SurfaceAuthentication.Administration)
        {
            AddOperatorPermissions(principal, claims);
        }

        return ValueTask.FromResult(HostSurfaceIdentityResult.Identified(
            SurfaceIdentityIssuers.Host,
            subjectId,
            "backend-session",
            now,
            // The derived identity lives no longer than the request's own session
            // horizon; the host clamps it again on normalisation.
            now.AddHours(1),
            ResolveDisplayName(principal),
            claims));
    }

    /// <summary>
    /// Turns <c>permission</c> claims into surface claims (ADR-023). The split is lossless and
    /// needs no mapping table: <c>communication.calls.read</c> is the function
    /// <c>communication.calls</c> with the action <c>read</c> — exactly the shape a view or a
    /// block asks for. A table would have been a second truth about the same permission, and the
    /// first entry someone forgot to add would make a surface silently mute.
    /// </summary>
    /// <remarks>
    /// The wildcard <c>*</c> (API keys, see <c>ApiKeyAuthenticationHandler</c>) is NOT expanded:
    /// it has no function part to derive, and inventing "every claim" here would mean the surface
    /// side had to learn wildcard semantics too — a second place answering the same question. A
    /// machine key therefore carries no surface claims, which is the safe direction.
    /// </remarks>
    private static void AddOperatorPermissions(
        ClaimsPrincipal principal,
        Dictionary<string, IReadOnlyList<string>> claims)
    {
        var actionsByFunction = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var permission in principal.FindAll(BackendClaimTypes.Permission))
        {
            if (!BackendPermissionKey.TryParse(permission.Value, out var key))
            {
                continue;
            }

            if (!actionsByFunction.TryGetValue(key.Function, out var actions))
            {
                actions = [];
                actionsByFunction[key.Function] = actions;
            }

            if (!actions.Contains(key.Action, StringComparer.Ordinal))
            {
                actions.Add(key.Action);
            }
        }

        foreach (var (function, actions) in actionsByFunction)
        {
            // The workspace binding is an assertion about WHO this is; a permission must never
            // be able to state it. Guarding on "is it already there?" would not be enough: with
            // no binding present, a permission named after it would MINT one, and the tenancy
            // boundary would hang off whatever an operator called their role.
            if (string.Equals(function, WorkspaceClaim, StringComparison.Ordinal))
            {
                continue;
            }

            if (!claims.ContainsKey(function))
            {
                claims[function] = actions;
            }
        }
    }

    private static string? ResolveSubjectId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub")
        ?? principal.Identity?.Name;

    private static string? ResolveDisplayName(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.FindFirstValue("name")
        ?? principal.FindFirstValue(ClaimTypes.Email);
}
