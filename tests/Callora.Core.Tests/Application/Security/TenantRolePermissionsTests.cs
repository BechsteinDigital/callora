using Callora.Core.Application.Security;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Was ein TenantAdmin darf — und vor allem, was er nicht darf.
/// </summary>
/// <remarks>
/// Der Fall, für den die Ebene existiert: Eine Agentur betreibt die Instanz, ihre Kunden betreiben
/// die Mandanten. Der Satz muss groß genug sein, dass ein Kunde sein eigenes Haus verwaltet, und
/// klein genug, dass er weder das Nachbarhaus noch das Gebäude erreicht.
/// </remarks>
public sealed class TenantRolePermissionsTests
{
    [Fact]
    public void Admin_ManagesTheOwnHouse()
    {
        var permissions = TenantRolePermissions.ForRole(BackendRoles.Admin);

        Assert.Contains(BackendPermissionKeys.WorkspaceRead, permissions);
        Assert.Contains(BackendPermissionKeys.MembershipUpdate, permissions);
        Assert.Contains(BackendPermissionKeys.PluginRead, permissions);

        // Der Punkt der Ebene: Der Mandant entscheidet, welcher seiner Workspaces welches
        // lizenzierte Plugin nutzt — ohne an der Installation drehen zu dürfen.
        Assert.Contains(BackendPermissionKeys.PluginAssign, permissions);
    }

    [Fact]
    public void Admin_NeverInstallsOrRemovesAPluginOnTheHost()
    {
        var permissions = TenantRolePermissions.ForRole(BackendRoles.Admin);

        // Die beiden bedeuten "Artefakt auf dem Host" — eine Binary, eine Version, ein Schema für
        // alle Mandanten dieser Instanz. Ein Kunde, der sie hätte, zöge Fremdcode in den Prozess,
        // in dem die anderen Kunden mitlaufen.
        Assert.DoesNotContain(BackendPermissionKeys.PluginCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.PluginDelete, permissions);
    }

    [Fact]
    public void Admin_NeverGetsWildcardOrPlatformPermissions()
    {
        var permissions = TenantRolePermissions.ForRole(BackendRoles.Admin);

        Assert.DoesNotContain("*", permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.RoleUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.ConfigUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.ExtensionUpdate, permissions);
    }

    [Fact]
    public void Admin_DoesNotCreateWorkspacesYet()
    {
        var permissions = TenantRolePermissions.ForRole(BackendRoles.Admin);

        // workspace.create schriebe Workspace.TenantId — genau das Feld, das der Write-Backstop in
        // HostPersistenceDbContext nicht prüfen kann, weil er Werte vergleicht und keine
        // Beziehungen. Bis der Endpunkt die Mandantenbindung selbst erzwingt, wäre das Recht ein Weg,
        // einen Workspace unter einem fremden Mandanten anzulegen.
        Assert.DoesNotContain(BackendPermissionKeys.WorkspaceCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.WorkspaceUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.WorkspaceDelete, permissions);
    }

    [Fact]
    public void Admin_NeverWritesToTheGlobalUser()
    {
        var permissions = TenantRolePermissions.ForRole(BackendRoles.Admin);

        // Wie im Workspace (#102): Diese Rechte wirken auf den globalen BackendUser — Zugangsdaten,
        // Löschung, Auskunft — und reichen damit in jeden Mandanten, dem die Person sonst angehört.
        Assert.DoesNotContain(BackendPermissionKeys.UserCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserDelete, permissions);
    }

    [Fact]
    public void Member_IsReadOnly()
    {
        var permissions = TenantRolePermissions.ForRole("member");

        Assert.Contains(BackendPermissionKeys.WorkspaceRead, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.MembershipUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserRead, permissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-custom")]
    public void UnknownRole_FallsBackToReadOnlyFloor(string? role)
    {
        var permissions = TenantRolePermissions.ForRole(role);

        Assert.DoesNotContain("*", permissions);
        Assert.DoesNotContain(BackendPermissionKeys.MembershipUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserRead, permissions);
    }

    [Fact]
    public void TheGrantableSetIsTheAdminSet()
    {
        // Die Obergrenze für anderswo zugewiesene Rollen: Ohne sie brächte ein "*" aus einer
        // globalen Rolle eine Mandanten-Sitzung über ihre Ebene hinaus.
        Assert.Equal(
            TenantRolePermissions.ForRole(BackendRoles.Admin).ToHashSet(StringComparer.Ordinal),
            TenantRolePermissions.TenantGrantable);
    }
}
