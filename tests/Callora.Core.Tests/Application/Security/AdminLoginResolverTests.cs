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

    [Fact]
    public async Task TenantMember_WithoutAWorkspace_GetsTenantScope()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(
            carol!, workspaceKey: null, userStore, rbacStore, options, tenantKey: "tenant-a");

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Tenant, grant!.Scope);
        Assert.Equal("tenant-a", grant.TenantKey);
        Assert.Null(grant.WorkspaceKey);
        Assert.Equal(TenantRolePermissions.ForRole(BackendRoles.Admin), grant.Permissions);
    }

    /// <summary>Wer einen Workspace nennt, will darin arbeiten — auch als Mandanten-Mitglied.</summary>
    /// <remarks>
    /// Die Mandantenebene ist kein Oberbegriff, der eine Workspace-Anmeldung ersetzt: Sie verwaltet,
    /// sie arbeitet nicht. Führe der Mandant, säße jemand mit beiden Mitgliedschaften plötzlich in
    /// einer Sitzung, die seine Medien und Flows nicht mehr sieht — herabgestuft, ohne es zu wollen.
    /// </remarks>
    [Fact]
    public async Task ANamedWorkspaceWins_EvenForATenantMember()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(
            carol!, "workspace-a", userStore, rbacStore, options, tenantKey: "tenant-a");

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Workspace, grant!.Scope);
        Assert.Equal("workspace-a", grant.WorkspaceKey);
        Assert.Null(grant.TenantKey);
    }

    /// <summary>Eine Mandanten-Mitgliedschaft öffnet keinen fremden Workspace.</summary>
    /// <remarks>
    /// Fail-closed: Ob ein TenantAdmin in jeden Workspace seines Mandanten darf, ist eine eigene
    /// Entscheidung mit eigener Prüfung. Bis sie getroffen ist, gilt die Mitgliedschaft.
    /// </remarks>
    [Fact]
    public async Task ATenantMember_DoesNotGetIntoAWorkspaceTheyDoNotBelongTo()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(
            carol!, "workspace-fremd", userStore, rbacStore, options, tenantKey: "tenant-a");

        Assert.Null(grant);
    }

    [Fact]
    public async Task NonTenantMember_GetsNothing()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, workspaceKey: null, userStore, rbacStore, options, tenantKey: "tenant-fremd");

        Assert.Null(grant);
    }

    [Fact]
    public async Task ATenantMembershipNamedLikeAnOperatorRole_IsRefused()
    {
        // Die Mitgliedsrolle wird zum Rollen-Claim, und WorkspaceScopeEvaluator.IsOperator prüft auf
        // den Namen. Eine Mandanten-Mitgliedschaft, die so hieße, wäre der Betreiber der Instanz.
        var (userStore, rbacStore, options) = await SetupAsync();
        userStore.AddTenantMember("tenant-a", "alice", BackendRoles.SuperAdmin);
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, workspaceKey: null, userStore, rbacStore, options, tenantKey: "tenant-a");

        Assert.Null(grant);
    }

    [Fact]
    public async Task Operator_IsNeverDownScopedToATenant()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var root = await userStore.GetByExternalIdAsync("root");

        var grant = await AdminLoginResolver.ResolveAsync(
            root!, workspaceKey: null, userStore, rbacStore, options, tenantKey: "tenant-a");

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Platform, grant!.Scope);
        Assert.Null(grant.TenantKey);
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
        userStore.AddTenantMember("tenant-a", "alice");
        userStore.AddTenantMember("tenant-a", "carol", BackendRoles.Admin);

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
            sessionPermissions: Session(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

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
            sessionPermissions: Session(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

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
            sessionPermissions: Session([], new() { ["pbx"] = ["pbx.person.read"] }));

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
            sessionPermissions: Session(["pbx"], new() { ["pbx"] = ["pbx.person.read"] }));

        Assert.Empty(grant!.Permissions);
    }

    [Fact]
    public async Task Ein_Mitglied_mit_einer_zugewiesenen_Rolle_traegt_deren_Schluessel()
    {
        // Der Fall, für den es keinen Ort gab: „Darf die Telefonanlage benutzen, aber nichts ändern."
        // Die Mitgliedsrolle kannte zwei Antworten, fest im Code, und keine davon war diese.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            sessionPermissions: Session(
                active: ["pbx"],
                byPlugin: new() { ["pbx"] = ["pbx.person.read", "pbx.person.update"] },
                assigned: new() { ["alice"] = ["pbx.viewer"] },
                roleGrants: new() { ["pbx.viewer"] = ["pbx.person.read"] }));

        Assert.Contains("pbx.person.read", grant!.Permissions);
        // Nur was in der Rolle steht — nicht alles, was das Plugin kann.
        Assert.DoesNotContain("pbx.person.update", grant.Permissions);
    }

    [Fact]
    public async Task Mehrere_zugewiesene_Rollen_zaehlen_zusammen()
    {
        // Der zweite Teil der Anforderung: „PBX lesen" und „Medien verwalten" sind zwei
        // Entscheidungen, und eine Person kann beide brauchen.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            sessionPermissions: Session(
                active: ["pbx"],
                byPlugin: new() { ["pbx"] = ["pbx.person.read"] },
                assigned: new() { ["alice"] = ["pbx.viewer", "medien"] },
                roleGrants: new()
                {
                    ["pbx.viewer"] = ["pbx.person.read"],
                    ["medien"] = [BackendPermissionKeys.MediaManage]
                }));

        Assert.Contains("pbx.person.read", grant!.Permissions);
        Assert.Contains(BackendPermissionKeys.MediaManage, grant.Permissions);
    }

    [Fact]
    public async Task Eine_zugewiesene_Rolle_bringt_keine_Plattform_Rechte_mit()
    {
        // Rollen sind global. Ohne diesen Filter wäre das Zuweisen einer Rolle an eine Mitgliedschaft
        // der Weg, Plattform-Berechtigungen in eine Workspace-Sitzung zu bekommen — genau das, was der
        // frühe Ausstieg in BackendClaimsTransformation verhindert.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            sessionPermissions: Session(
                active: [],
                byPlugin: [],
                assigned: new() { ["alice"] = ["zuviel"] },
                roleGrants: new()
                {
                    ["zuviel"] = ["*", BackendPermissionKeys.TenantCreate, BackendPermissionKeys.UserDelete]
                }));

        Assert.DoesNotContain("*", grant!.Permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, grant.Permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserDelete, grant.Permissions);
    }

    [Fact]
    public async Task Eine_zugewiesene_Rolle_bringt_kein_Plugin_mit_das_hier_nicht_aktiv_ist()
    {
        // Dieselbe Grenze wie beim Administrator: Gefiltert wird nach Aktivierung. Sonst wäre eine
        // Rolle der Weg, die Rechte eines Plugins in einen Workspace zu tragen, der es nie bekommen
        // hat.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            sessionPermissions: Session(
                active: [],
                byPlugin: new() { ["pbx"] = ["pbx.person.read"] },
                assigned: new() { ["alice"] = ["pbx.viewer"] },
                roleGrants: new() { ["pbx.viewer"] = ["pbx.person.read"] }));

        Assert.DoesNotContain("pbx.person.read", grant!.Permissions);
    }

    [Fact]
    public async Task Eine_zugewiesene_Rolle_die_es_nicht_mehr_gibt_verhindert_keine_Anmeldung()
    {
        // Die Zuweisung wird beim Löschen der Rolle mitgenommen, aber sie kann auf anderem Weg
        // überleben. Nichts ist die richtige Antwort — nicht ein Fehler, der jemanden aussperrt.
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(
            alice!, "workspace-a", userStore, rbacStore, options,
            sessionPermissions: Session(
                active: [], byPlugin: [], assigned: new() { ["alice"] = ["weg"] }));

        Assert.NotNull(grant);
        // Als Menge verglichen: Der Dienst gibt sortiert zurück, die fest verdrahtete Liste in ihrer
        // Schreibreihenfolge. Auf die Reihenfolge sieht niemand — die Prüfung fragt nach Enthaltensein —,
        // und ein sortiertes Token ist zwischen zwei Anmeldungen wenigstens dasselbe.
        Assert.Equal(
            WorkspaceRolePermissions.ForRole("member").Order(StringComparer.Ordinal),
            grant!.Permissions);
    }

    private static WorkspaceSessionPermissions Session(
        IReadOnlyList<string> active,
        Dictionary<string, IReadOnlyList<string>> byPlugin,
        Dictionary<string, IReadOnlyList<string>>? assigned = null,
        Dictionary<string, IReadOnlyCollection<string>>? roleGrants = null)
        => new(
            new StubMembershipRoles(assigned ?? []),
            new StubRbac(roleGrants ?? []),
            new WorkspacePluginPermissions(new StubActivations(active), new StubMap(byPlugin)));

    private sealed class StubMembershipRoles(Dictionary<string, IReadOnlyList<string>> byUser)
        : IWorkspaceMembershipRoleStore
    {
        public Task<IReadOnlyList<string>> ListRolesAsync(
            string workspaceKey, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(byUser.TryGetValue(userId, out var roles) ? roles : []);

        public Task<IReadOnlyList<string>?> ReplaceRolesAsync(
            string workspaceKey,
            string userId,
            IReadOnlyCollection<string> roles,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Diese Tests weisen nichts zu.");

        public Task<IReadOnlyList<string>> ListUsersWithRoleAsync(
            string role, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Diese Tests widerrufen nichts.");
    }

    private sealed class StubRbac(Dictionary<string, IReadOnlyCollection<string>> roles) : IBackendRbacStore
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetRolePermissionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(roles);

        public Task<IReadOnlyCollection<string>?> GetRolePermissionsAsync(
            string role, CancellationToken cancellationToken = default)
            => Task.FromResult(roles.TryGetValue(role, out var permissions) ? permissions : null);

        public Task UpsertRoleAsync(
            string role, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RemoveRoleAsync(string role, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> GetUserRolesAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string?> GetUserRoleAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpsertUserRoleAsync(
            string userId, string role, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RemoveUserRoleAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

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
