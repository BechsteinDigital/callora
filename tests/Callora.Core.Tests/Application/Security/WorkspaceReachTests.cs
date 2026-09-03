using Callora.Core.Application.Security;
using Callora.Core.Tests.Support;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Wer darf auf einen genannten Workspace wirken — über alle drei Scopes.
/// </summary>
/// <remarks>
/// <b>Der Befund:</b> <c>WorkspacePluginsController</c> nahm den Workspace-Schlüssel aus der URL und
/// fragte nie, ob der Aufrufer ihn erreichen darf. Das blieb folgenlos, solange nur Operatoren
/// <c>plugin.execute</c> hielten — und wurde in dem Moment scharf, in dem das Recht enger vergeben
/// wird. Genau das tut dieser Zweig: Der Mandanten-Administrator bekommt <c>plugin.assign</c>.
/// </remarks>
public sealed class WorkspaceReachTests
{
    [Fact]
    public async Task AnOperator_ReachesEveryWorkspace()
    {
        var reach = await ReachAsync();

        Assert.True(await reach.CanReachAsync(Session(BackendAuthScopes.Platform), "workspace-b"));
    }

    [Fact]
    public async Task AWorkspaceSession_ReachesOnlyItsOwn()
    {
        var reach = await ReachAsync();
        var session = Session(BackendAuthScopes.Workspace, workspaceKey: "workspace-a");

        Assert.True(await reach.CanReachAsync(session, "workspace-a"));
        Assert.False(await reach.CanReachAsync(session, "workspace-b"));
    }

    [Fact]
    public async Task ATenantSession_ReachesTheWorkspacesOfItsTenant()
    {
        var reach = await ReachAsync();
        var session = Session(BackendAuthScopes.Tenant, tenantKey: "tenant-a");

        Assert.True(await reach.CanReachAsync(session, "workspace-a"));
        Assert.True(await reach.CanReachAsync(session, "workspace-a2"));
    }

    [Fact]
    public async Task ATenantSession_DoesNotReachAnotherTenantsWorkspace()
    {
        // Der Fall, für den der Dienst existiert: Eine Agentur betreibt die Instanz, ihre Kunden
        // sind Mandanten. Ohne diese Prüfung nennte ein Kunde einfach den Workspace-Schlüssel des
        // Nachbarn in der URL.
        var reach = await ReachAsync();
        var session = Session(BackendAuthScopes.Tenant, tenantKey: "tenant-a");

        Assert.False(await reach.CanReachAsync(session, "workspace-b"));
    }

    [Fact]
    public async Task ATenantSessionWithoutItsKey_ReachesNothing()
    {
        var reach = await ReachAsync();

        Assert.False(await reach.CanReachAsync(Session(BackendAuthScopes.Tenant), "workspace-a"));
    }

    [Fact]
    public async Task AWorkspaceThatDoesNotExist_IsNotReachedByATenant()
    {
        var reach = await ReachAsync();
        var session = Session(BackendAuthScopes.Tenant, tenantKey: "tenant-a");

        Assert.False(await reach.CanReachAsync(session, "workspace-gibt-es-nicht"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnUnnamedWorkspace_IsNeverReached(string? workspaceKey)
    {
        // Fail-closed: Ein leerer Schlüssel ist keine Antwort, sondern eine fehlende Frage.
        var reach = await ReachAsync();
        var session = Session(BackendAuthScopes.Workspace, workspaceKey: "workspace-a");

        Assert.False(await reach.CanReachAsync(session, workspaceKey));
    }

    [Fact]
    public async Task ASessionWithoutAnyScope_ReachesNothing()
    {
        var reach = await ReachAsync();

        Assert.False(await reach.CanReachAsync(Session(scope: null), "workspace-a"));
    }

    private static async Task<WorkspaceReach> ReachAsync()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        store.AddTenant("tenant-b");
        await store.UpsertAsync("tenant-a", "workspace-a", "A", "default", isActive: true);
        await store.UpsertAsync("tenant-a", "workspace-a2", "A2", "default", isActive: true);
        await store.UpsertAsync("tenant-b", "workspace-b", "B", "default", isActive: true);
        return new WorkspaceReach(store);
    }

    private static ClaimsPrincipal Session(
        string? scope, string? workspaceKey = null, string? tenantKey = null)
    {
        var claims = new List<Claim>();
        if (scope is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.CalloraScope, scope));
        }

        if (workspaceKey is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, workspaceKey));
        }

        if (tenantKey is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.TenantKey, tenantKey));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
