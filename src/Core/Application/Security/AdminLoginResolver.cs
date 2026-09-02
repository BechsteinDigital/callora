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
    /// <param name="workspacePlugins">
    /// Was die Plugins dieses Workspace an Berechtigungen mitbringen. Ohne diesen Parameter bleibt es
    /// beim festen Kern-Satz — das Verhalten von vorher, für jeden von Hand zusammengesetzten Aufbau.
    /// </param>
    public static async Task<AdminLoginGrant?> ResolveAsync(
        BackendUser user,
        string? workspaceKey,
        IBackendUserStore userStore,
        IBackendRbacStore rbacStore,
        BackendHostOptions options,
        CancellationToken cancellationToken = default,
        WorkspacePluginPermissions? workspacePlugins = null)
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

        // Non-operators must name a workspace they belong to; the membership
        // role — not any global role — drives what the session may do inside it.
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
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
            Permissions: await WorkspacePermissionsAsync(
                    workspaceRole, trimmedKey, workspacePlugins, cancellationToken)
                .ConfigureAwait(false));
    }

    /// <summary>
    /// Was eine Workspace-Sitzung tragen darf: der feste Kern-Satz, und für den Administrator die
    /// Berechtigungen der Plugins, die in diesem Workspace aktiv sind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warum das überhaupt nötig war.</b> Eine Workspace-Sitzung bekam ihre Berechtigungen
    /// ausschließlich aus <see cref="WorkspaceRolePermissions"/> — einer festen Liste von
    /// Kern-Schlüsseln —, und die Projektion aus RBAC steigt für Workspace-Scope bewusst sofort aus. Ein
    /// Plugin-Schlüssel konnte damit auf keinem Weg in eine Workspace-Sitzung gelangen: Jede
    /// Plugin-Oberfläche war für alle außer dem Super-Admin leer, egal welche Rolle jemand hatte. Die
    /// Absicherung wirkte, die Vergabe war unmöglich.
    /// </para>
    /// <para>
    /// <b>Nur der Administrator, und nur seine Plugins.</b> Ein Mitglied behält den Leseboden. Der
    /// Administrator verwaltet seinen Workspace ohnehin vollständig — Abläufe, Medien, Webhooks,
    /// Mitgliedschaften —, und die Telefonanlage seines Workspace ist von derselben Art. Was er
    /// <em>nicht</em> bekommt, sind die Plugins anderer Workspaces: Gefiltert wird nach Aktivierung, nicht
    /// nach Installation.
    /// </para>
    /// <para>
    /// <b>Der Ausstieg in <c>BackendClaimsTransformation</c> bleibt, wie er ist.</b> Er verhindert, dass
    /// eine gleichnamige Plattformrolle auf eine Mitgliedschaft durchschlägt, und genau das soll er
    /// weiter tun. Die Erweiterung hier führt keine Rolle nach; sie erweitert den Satz, den die
    /// Mitgliedschaft ohnehin trägt, um das, was der Workspace an Plugins hat.
    /// </para>
    /// <para>
    /// Die Berechtigungen stehen im Token. Ein Plugin, das nach der Anmeldung aktiviert wird, wirkt
    /// deshalb erst bei der nächsten — dasselbe gilt für jede andere Rechteänderung, und der Weg dorthin
    /// ist derselbe: neu anmelden.
    /// </para>
    /// </remarks>
    private static async Task<IReadOnlyList<string>> WorkspacePermissionsAsync(
        string workspaceRole,
        string workspaceKey,
        WorkspacePluginPermissions? workspacePlugins,
        CancellationToken cancellationToken)
    {
        var core = WorkspaceRolePermissions.ForRole(workspaceRole);
        if (workspacePlugins is null
            || !string.Equals(workspaceRole.Trim(), BackendRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return core;
        }

        var fromPlugins = await workspacePlugins
            .ForWorkspaceAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        return fromPlugins.Count == 0 ? core : [.. core, .. fromPlugins];
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
