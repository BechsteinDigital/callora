using System.Security.Claims;
using Callora.Core.Application.Plugins;
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
    TimeProvider timeProvider,
    ICalloraPluginCatalog pluginCatalog,
    SurfaceIdentityOptions options)
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

        // Die Mandantengrenze: Eine an einen Workspace gebundene Sitzung authentifiziert nur
        // auf Flächen DIESES Workspaces. Ohne diese Zeile las die Methode zwar den gebundenen
        // Workspace, verglich ihn aber nie mit dem der Fläche — ein Workspace-Admin von A bekam
        // auf der Administrations-Fläche von B eine gültige Identität, das Gate sah einen
        // authentifizierten Aufrufer und servierte. Ohne eigene Domain ist die fremde Fläche
        // dabei nur einen Pfadwechsel entfernt (ADR-021).
        //
        // Dieselbe Regel wie im Backend, nicht eine zweite: Plattform-Operatoren sind nicht
        // gebunden und kommen überall herein, alle anderen nur in ihren eigenen Workspace.
        if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(principal, request.WorkspaceKey))
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
            AddOperatorPermissions(principal, claims, pluginCatalog, options.MaxClaimKeyLength);
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
        Dictionary<string, IReadOnlyList<string>> claims,
        ICalloraPluginCatalog pluginCatalog,
        int maxClaimKeyLength)
    {
        // Ein SuperAdmin trägt KEINE Berechtigungs-Claims — er umgeht im Backend jede Prüfung
        // über seine Rolle (`EndpointAuthorizationExtensions.HasPermission`). Ohne diesen Zweig
        // brächte ausgerechnet der Betreiber der Anlage auf seiner eigenen Fläche nichts mit,
        // während ein Mitarbeiter mit weniger Rechten dort mehr könnte als er.
        var source = principal.IsInRole(BackendRoles.SuperAdmin)
            ? BackendPermissionInventory.All(pluginCatalog).Select(value => new Claim(BackendClaimTypes.Permission, value))
            : principal.FindAll(BackendClaimTypes.Permission);

        var actionsByFunction = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var permission in source)
        {
            if (!BackendPermissionKey.TryParse(permission.Value, out var key))
            {
                continue;
            }

            // Ein Flächen-Claim braucht einen Namensraum (`crm.roles`), eine Kern-Berechtigung
            // nicht: `config.read` ergibt die Funktion `config` — einsegmentig und damit als
            // Claim-Schlüssel unzulässig. Übersprungen statt durchgereicht, weil die
            // Normalisierung sonst die GANZE Identität verwirft und die Fläche mit 503 antwortet:
            // Ein Schlüssel, den die Fläche gar nicht führen kann, darf keine Anmeldung kippen.
            if (!SurfaceIdentityTokenSyntax.IsNamespacedKey(key.Function, maxClaimKeyLength))
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
