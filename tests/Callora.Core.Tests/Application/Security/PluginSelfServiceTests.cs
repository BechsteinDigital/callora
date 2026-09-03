using Callora.Core.Application.Security;
using Callora.Core.Tests.Support;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Ob ein Workspace-Administrator sich selbst bedienen darf — oder ob der Mandant die Entscheidung
/// behalten hat.
/// </summary>
/// <remarks>
/// Die Schranke, die <c>plugin.assign</c> im Workspace-Satz überhaupt tragbar macht. Ohne sie hieße
/// der Schlüssel: Jeder Workspace nimmt sich, was der Mandant lizenziert hat, und der Mandant
/// erfährt es, wenn es passiert ist.
/// </remarks>
public sealed class PluginSelfServiceTests
{
    [Fact]
    public async Task AWorkspaceAdmin_MayNotAssignWhatTheTenantHasNotDelegated()
    {
        var gate = Gate(out _);

        Assert.False(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", "pbx"));
    }

    [Fact]
    public async Task AWorkspaceAdmin_MayAssignWhatTheTenantDelegated()
    {
        var gate = Gate(out var delegations);
        await delegations.SetAsync("tenant-a", "pbx", workspacesMayAssign: true, updatedBy: null);

        Assert.True(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", "pbx"));
    }

    [Fact]
    public async Task DelegationIsPerPlugin_NotPerTenantWholesale()
    {
        var gate = Gate(out var delegations);
        await delegations.SetAsync("tenant-a", "pbx", workspacesMayAssign: true, updatedBy: null);

        Assert.True(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", "pbx"));
        Assert.False(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", "videoconference"));
    }

    [Fact]
    public async Task TakingItBack_ClosesItAgain()
    {
        var gate = Gate(out var delegations);
        await delegations.SetAsync("tenant-a", "pbx", workspacesMayAssign: true, updatedBy: null);
        await delegations.SetAsync("tenant-a", "pbx", workspacesMayAssign: false, updatedBy: null);

        Assert.False(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", "pbx"));
    }

    /// <summary>Die Regel meint Workspace-Sitzungen — wer sie trifft, unterliegt ihr nicht.</summary>
    [Theory]
    [InlineData(BackendAuthScopes.Platform)]
    [InlineData(BackendAuthScopes.Tenant)]
    public async Task WhoeverMakesTheDecision_IsNotBoundByIt(string scope)
    {
        var gate = Gate(out _);
        var session = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(BackendClaimTypes.CalloraScope, scope)], authenticationType: "Test"));

        Assert.True(await gate.IsAllowedAsync(session, "workspace-a", "pbx"));
    }

    [Fact]
    public async Task AnUnknownWorkspace_IsRefused()
    {
        // Fail-closed: Ohne Workspace gibt es keinen Mandanten, und ohne Mandanten keine Delegation.
        var gate = Gate(out var delegations);
        await delegations.SetAsync("tenant-a", "pbx", workspacesMayAssign: true, updatedBy: null);

        Assert.False(await gate.IsAllowedAsync(WorkspaceSession(), "gibt-es-nicht", "pbx"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AMissingPluginId_IsRefused(string? pluginId)
    {
        var gate = Gate(out _);

        Assert.False(await gate.IsAllowedAsync(WorkspaceSession(), "workspace-a", pluginId));
    }

    private static PluginSelfService Gate(out InMemoryTenantPluginDelegationStore delegations)
    {
        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant("tenant-a");
        workspaces.UpsertAsync("tenant-a", "workspace-a", "A", "default", isActive: true)
            .GetAwaiter()
            .GetResult();
        delegations = new InMemoryTenantPluginDelegationStore();
        return new PluginSelfService(workspaces, delegations);
    }

    private static ClaimsPrincipal WorkspaceSession() =>
        new(new ClaimsIdentity(
            [
                new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Workspace),
                new Claim(BackendClaimTypes.WorkspaceKey, "workspace-a"),
            ],
            authenticationType: "Test"));
}
