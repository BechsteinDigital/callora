using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.WorkspaceAssignments;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins.WorkspaceAssignments;

public sealed class WorkspacePluginAssignmentServiceTests
{
    [Fact]
    public async Task List_ReturnsGlobalEntitlementAndWorkspaceActivationState()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Entitlements.SetEntitledAsync(
            "videoconference",
            true,
            "acme",
            "tenant-a");
        await fixture.Activations.SetActiveAsync(
            "videoconference",
            "acme",
            "tenant-a",
            true);

        var result = await fixture.Service.ListAsync("acme");

        Assert.Equal(WorkspacePluginAssignmentStatus.Ok, result.Status);
        var item = Assert.Single(result.Items);
        Assert.Equal("videoconference", item.PluginId);
        Assert.True(item.IsGloballyActive);
        Assert.True(item.IsEntitled);
        Assert.True(item.IsActive);
        Assert.True(item.IsAssigned);
    }

    [Fact]
    public async Task Assign_ActivatesWorkspaceThenPersistsEntitlement()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.SetAssignedAsync(
            "acme",
            "videoconference",
            isAssigned: true,
            requestedBy: "operator");

        Assert.Equal(WorkspacePluginAssignmentStatus.Ok, result.Status);
        Assert.True(result.Assignment!.IsAssigned);
        var activation = Assert.Single(fixture.Lifecycle.ActivateCalls);
        Assert.Equal("acme", activation.WorkspaceKey);
        Assert.Equal("operator", activation.RequestedBy);
        Assert.True(await fixture.Entitlements.IsEntitledAsync(
            "videoconference",
            "acme",
            "tenant-a"));
    }

    [Fact]
    public async Task Assign_WhenCapabilityGuardRejects_DoesNotGrantEntitlement()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Lifecycle.ActivateResult = new PluginLifecycleServiceResult(
            PluginLifecycleServiceStatus.BadRequest,
            false,
            Message: "Required capability 'communication.foundation' is missing.",
            ErrorCode: PluginLifecycleErrorCodes.PluginRequiredCapabilityMissing);

        var result = await fixture.Service.SetAssignedAsync(
            "acme",
            "videoconference",
            isAssigned: true,
            requestedBy: "operator");

        Assert.Equal(WorkspacePluginAssignmentStatus.LifecycleRejected, result.Status);
        Assert.Equal(
            PluginLifecycleErrorCodes.PluginRequiredCapabilityMissing,
            result.ErrorCode);
        Assert.False(await fixture.Entitlements.IsEntitledAsync(
            "videoconference",
            "acme",
            "tenant-a"));
    }

    [Fact]
    public async Task Unassign_DeactivatesWorkspaceThenRevokesEntitlement()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Entitlements.SetEntitledAsync(
            "videoconference",
            true,
            "acme",
            "tenant-a");
        await fixture.Activations.SetActiveAsync(
            "videoconference",
            "acme",
            "tenant-a",
            true);

        var result = await fixture.Service.SetAssignedAsync(
            "acme",
            "videoconference",
            isAssigned: false,
            requestedBy: "operator");

        Assert.Equal(WorkspacePluginAssignmentStatus.Ok, result.Status);
        Assert.False(result.Assignment!.IsAssigned);
        Assert.Single(fixture.Lifecycle.DeactivateCalls);
        Assert.False(await fixture.Entitlements.IsEntitledAsync(
            "videoconference",
            "acme",
            "tenant-a"));
    }

    [Fact]
    public async Task Assign_InactiveGlobalPlugin_ReturnsActionableFailure()
    {
        var fixture = await CreateFixtureAsync(PluginInstallationState.Inactive);

        var result = await fixture.Service.SetAssignedAsync(
            "acme",
            "videoconference",
            isAssigned: true,
            requestedBy: "operator");

        Assert.Equal(WorkspacePluginAssignmentStatus.PluginInactive, result.Status);
        Assert.Empty(fixture.Lifecycle.ActivateCalls);
        Assert.Contains("globally active", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_UnknownWorkspace_ReturnsNotFound()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.ListAsync("missing");

        Assert.Equal(WorkspacePluginAssignmentStatus.WorkspaceNotFound, result.Status);
        Assert.Empty(result.Items);
    }

    private static async Task<WorkspacePluginAssignmentServiceTestFixture> CreateFixtureAsync(
        PluginInstallationState state = PluginInstallationState.Active)
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync(
            "tenant-a",
            "acme",
            "Acme",
            "standard",
            isActive: true);

        var lifecycle = new ConfigurablePluginLifecycleService();
        lifecycle.Installations.Add(new PluginInstallationSnapshot(
            "videoconference",
            "Video Conference",
            "/plugins/videoconference.dll",
            null,
            (int)state,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        var entitlements = new InMemoryPluginEntitlementStore(
            new BackendHostOptions { DefaultPluginEntitlement = false });
        var activations = new InMemoryWorkspacePluginActivationStore();
        var service = new WorkspacePluginAssignmentService(
            workspaceStore,
            lifecycle,
            activations,
            entitlements,
            NullLogger<WorkspacePluginAssignmentService>.Instance);
        return new WorkspacePluginAssignmentServiceTestFixture(
            service,
            lifecycle,
            entitlements,
            activations);
    }
}
