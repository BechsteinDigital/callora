namespace Callora.Core.Application.Security;

/// <summary>
/// Was eine Workspace-Sitzung tragen darf: der Boden ihrer Mitgliedsrolle, die Plugins ihres
/// Workspace, und was ihr an Rollen zugewiesen wurde.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum es das braucht.</b> Eine Mitgliedschaft kannte genau zwei Antworten, fest im Code:
/// Administrator oder Mitglied. „Darf die Telefonanlage benutzen, aber nichts ändern" ließ sich nicht
/// sagen — es gab keinen Ort, an dem es hätte stehen können, und keinen Weg, an dem es entlanggekommen
/// wäre.
/// </para>
/// <para>
/// <b>Drei Quellen, eine Obergrenze.</b> Der Boden kommt aus <see cref="WorkspaceRolePermissions"/>.
/// Der Administrator bekommt zusätzlich die Schlüssel der Plugins seines Workspace. Und jede
/// zugewiesene Rolle bringt mit, was in ihr steht — gefiltert auf das, was in diesem Workspace
/// überhaupt gelten darf: die Kern-Schlüssel, die ein Workspace-Administrator halten dürfte, und die
/// Schlüssel der hier aktiven Plugins. Alles andere fällt weg, insbesondere <c>*</c>.
/// </para>
/// <para>
/// <b>Der Filter ist nicht Vorsicht, sondern die Bedingung.</b> Rollen sind global; ohne ihn wäre das
/// Zuweisen einer Rolle an eine Mitgliedschaft ein Weg, Plattform-Berechtigungen in eine
/// Workspace-Sitzung zu bekommen — genau das, was der frühe Ausstieg in
/// <c>BackendClaimsTransformation</c> verhindert.
/// </para>
/// </remarks>
public sealed class WorkspaceSessionPermissions(
    IWorkspaceMembershipRoleStore memberships,
    IBackendRbacStore rbac,
    WorkspacePluginPermissions workspacePlugins)
{
    private readonly IWorkspaceMembershipRoleStore _memberships =
        memberships ?? throw new ArgumentNullException(nameof(memberships));

    private readonly IBackendRbacStore _rbac = rbac ?? throw new ArgumentNullException(nameof(rbac));

    private readonly WorkspacePluginPermissions _workspacePlugins =
        workspacePlugins ?? throw new ArgumentNullException(nameof(workspacePlugins));

    /// <summary>Die Berechtigungen, mit denen die Sitzung ausgestellt wird.</summary>
    public async Task<IReadOnlyList<string>> ForAsync(
        string workspaceKey,
        string userId,
        string? membershipRole,
        CancellationToken cancellationToken = default)
    {
        var effective = new SortedSet<string>(
            WorkspaceRolePermissions.ForRole(membershipRole), StringComparer.Ordinal);

        var fromPlugins = await _workspacePlugins
            .ForWorkspaceAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        var isAdmin = string.Equals(
            membershipRole?.Trim(), BackendRoles.Admin, StringComparison.OrdinalIgnoreCase);

        if (isAdmin)
        {
            effective.UnionWith(fromPlugins);
        }

        var assigned = await AssignedAsync(workspaceKey, userId, cancellationToken).ConfigureAwait(false);
        if (assigned.Count > 0)
        {
            // Die Obergrenze, einmal berechnet: Kern-Schlüssel, die hier gelten dürfen, plus die
            // Schlüssel der Plugins, die dieser Workspace aktiviert hat.
            var allowed = new HashSet<string>(WorkspaceRolePermissions.WorkspaceGrantable, StringComparer.Ordinal);
            allowed.UnionWith(fromPlugins);

            effective.UnionWith(assigned.Where(allowed.Contains));
        }

        return [.. effective];
    }

    /// <summary>Was in den zugewiesenen Rollen steht, bevor gefiltert wird.</summary>
    private async Task<IReadOnlyCollection<string>> AssignedAsync(
        string workspaceKey, string userId, CancellationToken cancellationToken)
    {
        var roles = await _memberships
            .ListRolesAsync(workspaceKey, userId, cancellationToken)
            .ConfigureAwait(false);

        if (roles.Count == 0)
        {
            return [];
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            var permissions = await _rbac.GetRolePermissionsAsync(role, cancellationToken).ConfigureAwait(false);
            // Eine Rolle, die es nicht mehr gibt: Die Zuweisung wird beim Löschen mitgenommen, aber ein
            // In-Memory-Store oder eine Zuweisung aus einem anderen Weg kann sie überleben. Nichts ist
            // die richtige Antwort — nicht ein Fehler, der eine Anmeldung verhindert.
            if (permissions is not null)
            {
                keys.UnionWith(permissions);
            }
        }

        return keys;
    }
}
