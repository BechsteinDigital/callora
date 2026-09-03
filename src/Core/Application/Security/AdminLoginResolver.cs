using Callora.Core.Application.Policies;
using Callora.Core.Domain.Security;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Resolves the effective admin session for an authenticated user, unifying the
/// platform-operator and workspace-admin logins into one admin login
/// (ADR-014 §3.3, §14). Fail-closed: returns <c>null</c> (→ 403) unless the user
/// is a platform operator or a member of the requested workspace.
/// <para>
/// A platform operator always receives a platform-scoped session; the workspace
/// key is ignored for operators — they are never down-scoped. Every other user
/// must name a workspace they belong to and receives a workspace-scoped session
/// carrying the least-privilege permission set of their membership role
/// (<see cref="WorkspaceRolePermissions"/>).
/// </para>
/// </summary>
[CalloraInternal("Login-scope resolution — enforcement, not a plugin contract")]
public static class AdminLoginResolver
{
    /// <summary>
    /// Determines the session grant for <paramref name="user"/>, optionally
    /// targeting <paramref name="workspaceKey"/>. Returns <c>null</c> when the
    /// user is neither a platform operator nor a member of a named workspace.
    /// </summary>
    /// <param name="sessionPermissions">
    /// Was diese Sitzung tragen darf — Boden, Plugins des Workspace, zugewiesene Rollen. Ohne diesen
    /// Parameter bleibt es beim festen Kern-Satz der Mitgliedsrolle: das Verhalten von vorher, für jeden
    /// von Hand zusammengesetzten Aufbau.
    /// </param>
    public static async Task<AdminLoginGrant?> ResolveAsync(
        BackendUser user,
        string? workspaceKey,
        IBackendUserStore userStore,
        IBackendRbacStore rbacStore,
        BackendHostOptions options,
        CancellationToken cancellationToken = default,
        WorkspaceSessionPermissions? sessionPermissions = null,
        string? tenantKey = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(userStore);
        ArgumentNullException.ThrowIfNull(rbacStore);
        ArgumentNullException.ThrowIfNull(options);

        var globalRole = await rbacStore
            .GetUserRoleAsync(user.ExternalId, cancellationToken)
            .ConfigureAwait(false);
        if (IsPlatformOperatorRole(options, globalRole))
        {
            // The grant carries no permissions by design: super admins bypass
            // permission checks by role, and any other operator role has its
            // permissions projected from RBAC at request time
            // (BackendClaimsTransformation). Platform scope = reach, not authority.
            return new AdminLoginGrant(
                Scope: BackendAuthScopes.Platform,
                WorkspaceKey: null,
                Role: globalRole,
                Permissions: Array.Empty<string>());
        }

        // Ein benannter Workspace entscheidet, auch wenn die Person zugleich Mandanten-Mitglied ist:
        // Wer einen Workspace nennt, will darin arbeiten. Die Mandantenebene ist kein Oberbegriff,
        // der eine Workspace-Anmeldung ersetzt — sie verwaltet, sie arbeitet nicht.
        //
        // Umgekehrt wird niemand hochgestuft: Eine Mandanten-Mitgliedschaft öffnet keinen Workspace,
        // dem die Person nicht angehört. Das wäre eine eigene Entscheidung mit eigener Prüfung, und
        // fail-closed heißt hier: erst wenn sie getroffen ist.
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return string.IsNullOrWhiteSpace(tenantKey)
                ? null
                : await ResolveTenantAsync(user, tenantKey, userStore, options, cancellationToken)
                    .ConfigureAwait(false);
        }

        var trimmedKey = workspaceKey.Trim();
        var workspaceRole = await userStore
            .GetWorkspaceRoleAsync(user.ExternalId, trimmedKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspaceRole is null)
        {
            return null;
        }

        // Fail-closed gegen einen Rollennamen, den sich ein Workspace-Admin selbst geben kann.
        // Der Schreibpfad weist ihn inzwischen ab, aber dieser Zweig ist der wirksame: Er gilt
        // auch für Zeilen, die vor der Sperre entstanden sind, und für jeden künftigen Weg, auf
        // dem eine Mitgliedschaft in die Datenbank kommt (Migration, Seed, Direktzugriff).
        if (ReservedMembershipRoles.IsReserved(workspaceRole, options))
        {
            return null;
        }

        return new AdminLoginGrant(
            Scope: BackendAuthScopes.Workspace,
            WorkspaceKey: trimmedKey,
            Role: workspaceRole,
            Permissions: sessionPermissions is null
                ? WorkspaceRolePermissions.ForRole(workspaceRole)
                : await sessionPermissions
                    .ForAsync(trimmedKey, user.ExternalId, workspaceRole, cancellationToken)
                    .ConfigureAwait(false));
    }

    private static async Task<AdminLoginGrant?> ResolveTenantAsync(
        BackendUser user,
        string tenantKey,
        IBackendUserStore userStore,
        BackendHostOptions options,
        CancellationToken cancellationToken)
    {
        var trimmedKey = tenantKey.Trim();
        var tenantRole = await userStore
            .GetTenantRoleAsync(user.ExternalId, trimmedKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenantRole is null)
        {
            return null;
        }

        // Dieselbe Sperre wie im Workspace, und sie trägt hier genauso: Die Mitgliedsrolle wird zum
        // Rollen-Claim der Sitzung, und WorkspaceScopeEvaluator.IsOperator prüft auf den Namen
        // "superadmin". Eine Mandanten-Mitgliedschaft, die so hieße, wäre keine Verwaltung eines
        // Kunden mehr, sondern der Betreiber der Instanz.
        if (ReservedMembershipRoles.IsReserved(tenantRole, options))
        {
            return null;
        }

        return new AdminLoginGrant(
            Scope: BackendAuthScopes.Tenant,
            WorkspaceKey: null,
            Role: tenantRole,
            Permissions: TenantRolePermissions.ForRole(tenantRole),
            TenantKey: trimmedKey);
    }

    private static bool IsPlatformOperatorRole(BackendHostOptions options, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        foreach (var operatorRole in options.PlatformOperatorRoles ?? [])
        {
            if (string.Equals(operatorRole?.Trim(), role.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
