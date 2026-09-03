using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Maps a <see cref="Callora.Core.Domain.Tenants.TenantMembership"/> role to the permission set it
/// grants across the tenant's workspaces.
/// <para>
/// The level ADR-014 §18 calls the TenantAdmin: it administers a customer, not the instance. In the
/// agency case the agency runs the host and its customers run their tenants — so this set has to be
/// large enough that a customer manages their own house, and small enough that they never reach the
/// house next door or the building itself.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Kein <c>plugin.create</c>, kein <c>plugin.delete</c>.</b> Die beiden bedeuten „Artefakt auf dem
/// Host installieren bzw. entfernen" — eine Binary, eine Version, ein Schema für alle Mandanten
/// dieser Instanz. Wer sie einem Kunden gäbe, ließe ihn Fremdcode in den Prozess ziehen, in dem die
/// anderen Kunden mitlaufen. <c>plugin.read</c> genügt, um zu sehen, was verfügbar ist; das Zuweisen
/// an den eigenen Workspace ist eine eigene Frage und bekommt einen eigenen Schlüssel.
/// </para>
/// <para>
/// <b>Workspaces nur lesend, und das ist eine Entscheidung.</b> <c>workspace.create</c> schriebe
/// <c>Workspace.TenantId</c> — genau das Feld, das der Write-Backstop in
/// <c>HostPersistenceDbContext</c> nicht prüfen kann, weil er Werte vergleicht und keine
/// Beziehungen. Solange der Endpunkt die Mandantenbindung nicht selbst erzwingt, wäre das Recht ein
/// Weg, einen Workspace unter einem fremden Mandanten anzulegen. Es kommt dazu, wenn der Endpunkt
/// es trägt — nicht vorher.
/// </para>
/// <para>
/// <b>Keine Plattformschlüssel.</b> <c>tenant.*</c>, <c>role.*</c>, <c>extension.*</c>,
/// <c>config.update</c> und <c>user</c>-Schreibrechte bleiben beim Betreiber. Der letzte Punkt aus
/// demselben Grund wie im Workspace (#102): Sie wirken auf den globalen
/// <c>BackendUser</c> — Zugangsdaten, Löschung, Auskunft — und reichen damit in jeden Mandanten, dem
/// die betroffene Person sonst noch angehört.
/// </para>
/// </remarks>
[CalloraInternal("Tenant-role permission grants — RBAC enforcement, not a plugin contract (REV2 §7.2)")]
public static class TenantRolePermissions
{
    private static readonly IReadOnlyList<string> AdminPermissions =
    [
        BackendPermissionKeys.WorkspaceRead,
        BackendPermissionKeys.MembershipRead,
        BackendPermissionKeys.MembershipUpdate,
        BackendPermissionKeys.MembershipDelete,
        BackendPermissionKeys.UserRead,
        BackendPermissionKeys.PluginRead,
        BackendPermissionKeys.ConfigRead,
        BackendPermissionKeys.NotificationRead
    ];

    private static readonly IReadOnlyList<string> MemberPermissions =
    [
        BackendPermissionKeys.WorkspaceRead,
        BackendPermissionKeys.MembershipRead,
        BackendPermissionKeys.PluginRead,
        BackendPermissionKeys.ConfigRead
    ];

    /// <summary>
    /// Die Obergrenze dessen, was eine Mandanten-Sitzung überhaupt tragen darf.
    /// </summary>
    /// <remarks>
    /// Wie im Workspace ist der Satz des Administrators per Definition die Grenze: Eine anderswo
    /// zugewiesene Rolle wird dagegen gefiltert, sonst brächte ein <c>*</c> aus einer globalen Rolle
    /// eine Mandanten-Sitzung über ihre Ebene hinaus — und die Zuweisung wäre der Weg, genau das zu
    /// tun, was der frühe Ausstieg in <c>BackendClaimsTransformation</c> verhindert.
    /// </remarks>
    public static IReadOnlySet<string> TenantGrantable { get; } =
        AdminPermissions.ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Permissions for a tenant role. The tenant administrator gets the full tenant-scoped set;
    /// every other (including unknown) role gets the read-only floor — least privilege by default.
    /// </summary>
    public static IReadOnlyList<string> ForRole(string? tenantRole) =>
        string.Equals(tenantRole?.Trim(), BackendRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? AdminPermissions
            : MemberPermissions;
}
