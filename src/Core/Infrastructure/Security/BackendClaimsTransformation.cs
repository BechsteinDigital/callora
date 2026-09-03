using Callora.Core.Application.Security;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Projects RBAC role and permission claims from configured user-role assignments.
/// </summary>
[CalloraInternal("RBAC claims projection — not a plugin contract (REV2 §7.2)")]
public sealed class BackendClaimsTransformation(IBackendRbacStore rbacStore) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        // Rollennamen sind EIN Namensraum, Scopes sind zwei. Eine Workspace-Mitgliedschaft heißt
        // "admin" (BackendRoles.Admin), und eine Plattformrolle darf genauso heißen — gesperrt ist
        // nur "superadmin". Ohne diesen Ausstieg schlug die Projektion den Rollen-Claim der Session
        // nach, ohne auf den Scope zu sehen: Jeder Workspace-Admin JEDES Mandanten bekam damit die
        // Plattform-Permissions der gleichnamigen RBAC-Rolle. Dasselbe galt für die global
        // zugewiesene Rolle, die unten unabhängig vom Scope ergänzt wurde.
        //
        // Eine Workspace-Session verliert dabei nichts: Sie trägt ihre Permissions vollständig aus
        // WorkspaceRolePermissions im Token (AdminLoginResolver) — die Projektion ist der Weg für
        // Plattform-Operatoren, nicht für Mitgliedschaften.
        // Der Mandanten-Scope steigt aus demselben Grund aus: "admin" heißt eine Mitgliedschaft
        // dort genauso, und eine gleichnamige Plattformrolle würde jedem TenantAdmin JEDES
        // Mandanten deren Rechte geben. Auch er trägt seine Berechtigungen vollständig im Token
        // (TenantRolePermissions, gesetzt im AdminLoginResolver).
        //
        // Aufgezählt statt "alles außer platform": Es gibt authentifizierte Principals ohne
        // Scope-Claim, und für die IST die Projektion der Weg. Eine Negation würde ihnen still ihre
        // Rechte nehmen — ein Ausfall, der wie ein Rechteproblem aussieht und keins ist.
        if (principal.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Workspace) ||
            principal.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Tenant))
        {
            return principal;
        }

        var effectiveRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            effectiveRoles.Add(claim.Value);
        }

        foreach (var claim in principal.FindAll("role"))
        {
            effectiveRoles.Add(claim.Value);
        }

        var userId = ResolveUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var assignedRole = await rbacStore.GetUserRoleAsync(userId).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(assignedRole) && effectiveRoles.Add(assignedRole))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, assignedRole));
            }
        }

        foreach (var role in effectiveRoles)
        {
            var permissions = await rbacStore.GetRolePermissionsAsync(role).ConfigureAwait(false);
            if (permissions is null)
            {
                continue;
            }

            foreach (var permission in permissions)
            {
                if (!principal.HasClaim(BackendClaimTypes.Permission, permission))
                {
                    identity.AddClaim(new Claim(BackendClaimTypes.Permission, permission));
                }
            }
        }

        return principal;
    }

    private static string? ResolveUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("sub") ??
               principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
               principal.FindFirstValue(ClaimTypes.Name);
    }
}
