using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

public sealed class AdminLoginResolverTests
{
    [Fact]
    public async Task Operator_GetsPlatformScope_WorkspaceKeyIgnored()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var root = await userStore.GetByExternalIdAsync("root");

        var grant = await AdminLoginResolver.ResolveAsync(root!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Platform, grant!.Scope);
        Assert.Null(grant.WorkspaceKey);
        Assert.Empty(grant.Permissions);
    }

    [Fact]
    public async Task WorkspaceMember_WithWorkspaceKey_GetsWorkspaceScope()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Workspace, grant!.Scope);
        Assert.Equal("workspace-a", grant.WorkspaceKey);
    }

    [Fact]
    public async Task WorkspaceAdmin_GetsLeastPrivilegePermissions()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(carol!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendRoles.Admin, grant!.Role);
        Assert.Contains(BackendPermissionKeys.FlowManage, grant.Permissions);
        Assert.DoesNotContain("*", grant.Permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, grant.Permissions);
    }

    [Fact]
    public async Task NonOperator_WithoutWorkspaceKey_ReturnsNull()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, workspaceKey: null, userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task WorkspaceMember_ForeignWorkspace_ReturnsNull()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, "workspace-b", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Theory]
    [InlineData(BackendRoles.SuperAdmin)]
    [InlineData("SuperAdmin")]
    [InlineData("  superadmin  ")]
    [InlineData(BackendRoles.HostApi)]
    public async Task AMembershipRoleNamingAPlatformOperator_GrantsNothing(string role)
    {
        // Die Mitgliedsrolle ist ein FREIER String: Wer membership.update hat — jeder
        // Workspace-Admin (WorkspaceRolePermissions.AdminPermissions) — schreibt sie selbst.
        // Landete sie ungeprüft im Rollen-Claim, machte EndpointAuthorizationExtensions
        // daraus über `IsInRole(SuperAdmin)` unbeschränkten Plattformzugriff: aus
        // "Admin in EINEM Workspace" würde "Operator über ALLE".
        var (userStore, rbacStore, options) = await SetupAsync();
        userStore.AddWorkspaceMember("workspace-a", "mallory", role);
        await userStore.UpsertCredentialsAsync("mallory", "m@example.test", "Mallory", "pass-m");
        var mallory = await userStore.GetByExternalIdAsync("mallory");

        var grant = await AdminLoginResolver.ResolveAsync(mallory!, "workspace-a", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task AConfiguredOperatorRoleName_IsAlsoRefusedAsMembershipRole()
    {
        // Nicht nur "superadmin": Die Operator-Rollen sind konfigurierbar, und jede von
        // ihnen erreicht jeden Workspace (BackendHostOptions.PlatformOperatorRoles).
        var (userStore, rbacStore, options) = await SetupAsync();
        options.PlatformOperatorRoles = [BackendRoles.SuperAdmin, "plattform-betrieb"];
        userStore.AddWorkspaceMember("workspace-a", "mallory", "plattform-betrieb");
        await userStore.UpsertCredentialsAsync("mallory", "m@example.test", "Mallory", "pass-m");
        var mallory = await userStore.GetByExternalIdAsync("mallory");

        var grant = await AdminLoginResolver.ResolveAsync(mallory!, "workspace-a", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task AnOrdinaryMembershipRole_StillWorks()
    {
        // Die Gegenprobe: Die Sperre darf nur Operator-Namen treffen, nicht jede Rolle.
        var (userStore, rbacStore, options) = await SetupAsync();
        userStore.AddWorkspaceMember("workspace-a", "dave", "agent");
        await userStore.UpsertCredentialsAsync("dave", "d@example.test", "Dave", "pass-d");
        var dave = await userStore.GetByExternalIdAsync("dave");

        var grant = await AdminLoginResolver.ResolveAsync(dave!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Workspace, grant!.Scope);
        Assert.Equal("agent", grant.Role);
    }

    private static async Task<(InMemoryBackendUserStore UserStore, InMemoryBackendRbacStore RbacStore, BackendHostOptions Options)> SetupAsync()
    {
        var options = new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            RbacUserAssignments =
            [
                new BackendRbacUserAssignmentOptions { UserId = "root", Role = BackendRoles.SuperAdmin }
            ]
        };

        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync("root", "root@example.test", "Root", "pass-root");
        await userStore.UpsertCredentialsAsync("alice", "alice@example.test", "Alice", "pass-1");
        await userStore.UpsertCredentialsAsync("carol", "carol@example.test", "Carol", "pass-carol");
        userStore.AddWorkspaceMember("workspace-a", "alice");
        userStore.AddWorkspaceMember("workspace-a", "carol", BackendRoles.Admin);

        var rbacStore = new InMemoryBackendRbacStore(options);
        return (userStore, rbacStore, options);
    }

    [Fact]
    public async Task Ein_Workspace_Admin_traegt_die_Rechte_der_Plugins_seines_Workspace()
    {
        // Vorher konnte ein Plugin-Schlüssel auf KEINEM Weg in eine Workspace-Sitzung gelangen: Die
        // Rechte kommen aus einer fest verdrahteten Kern-Liste, und die Projektion aus RBAC steigt für
        // Workspace-Scope bewusst sofort aus. Jede Plugin-Oberfläche war damit für alle außer dem
        // Super-Admin leer — egal welche Rolle jemand hatte, egal welche Rolle die Installation anlegte.
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(
            carol!, "workspace-a", userStore, rbacStore, options,
            workspacePlugins: WorkspacePlugins(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

        Assert.Contains("pbx.person.read", grant!.Permissions);
        // Der Kern-Satz bleibt daneben stehen; die Erweiterung ersetzt ihn nicht.
        Assert.Contains(BackendPermissionKeys.FlowManage, grant.Permissions);
    }

    [Fact]
    public async Task Ein_Mitglied_bekommt_sie_nicht()
    {
        // Der Leseboden bleibt der Leseboden. Wer die Telefonanlage verwalten soll, wird Administrator
        // seines Workspace — feinere Zuschnitte brauchen Rollen, die das Plugin selbst benennt.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            workspacePlugins: WorkspacePlugins(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

        Assert.DoesNotContain("pbx.person.read", grant!.Permissions);
    }

    [Fact]
    public async Task Ein_Plugin_das_in_diesem_Workspace_nicht_aktiv_ist_gibt_nichts()
    {
        // Die Grenze, die das Ganze vertretbar macht: Gefiltert wird nach Aktivierung, nicht nach
        // Installation. Sonst trüge der Administrator eines Workspace die Rechte jedes Plugins der
        // Anlage — auch derer, die sein Workspace nie gesehen hat.
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(
            carol!, "workspace-a", userStore, rbacStore, options,
            workspacePlugins: WorkspacePlugins([], new() { ["pbx"] = ["pbx.person.read"] }));

        Assert.DoesNotContain("pbx.person.read", grant!.Permissions);
        Assert.Contains(BackendPermissionKeys.FlowManage, grant.Permissions);
    }

    [Fact]
    public async Task Ohne_die_Plugin_Quelle_bleibt_es_beim_Kern_Satz()
    {
        // Das Verhalten von vorher, für jeden von Hand zusammengesetzten Aufbau. Eine Anmeldung darf
        // nicht daran scheitern, dass ein Dienst fehlt, an dem sie nicht hängen sollte.
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(carol!, "workspace-a", userStore, rbacStore, options);

        Assert.Equal(WorkspaceRolePermissions.ForRole(BackendRoles.Admin), grant!.Permissions);
    }

    [Fact]
    public async Task Ein_Plattform_Operator_bekommt_weiterhin_keine_Rechte_ins_Token()
    {
        // Plattform-Scope heißt Reichweite, nicht Vollmacht: Seine Rechte werden bei jeder Anfrage aus
        // RBAC projiziert, und ein Workspace steht dabei gar nicht fest. Etwas hier hineinzuschreiben
        // hieße, die Plugins EINES Workspace in eine Sitzung zu schreiben, die für alle gilt.
        var (userStore, rbacStore, options) = await SetupAsync();
        var root = await userStore.GetByExternalIdAsync("root");

        var grant = await AdminLoginResolver.ResolveAsync(
            root!, "workspace-a", userStore, rbacStore, options,
            workspacePlugins: WorkspacePlugins(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

        Assert.Empty(grant!.Permissions);
    }

    private static WorkspacePluginPermissions WorkspacePlugins(
        IReadOnlyList<string> active, Dictionary<string, IReadOnlyList<string>> byPlugin)
        => new(new StubActivations(active), new StubMap(byPlugin));

    private sealed class StubActivations(IReadOnlyList<string> active)
        : Callora.Core.Application.Plugins.IWorkspacePluginActivationReader
    {
        public Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
            string workspaceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(active);
    }

    private sealed class StubMap(Dictionary<string, IReadOnlyList<string>> byPlugin) : IPluginPermissionMap
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(byPlugin);
    }
}
