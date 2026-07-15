using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;
using Callora.Host.PluginContracts.Application.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The evaluator gathers the availability factors from their owning stores.
/// Crucially, entitlement participates in the derived availability (P0-1): a
/// dropped entitlement makes the plugin unavailable while the workspace stays
/// enabled (desired activation preserved).
/// </summary>
public sealed class PluginAvailabilityEvaluatorTests
{
    private const string PluginId = "plugin-x";
    private const string WorkspaceKey = "workspace-a";
    private const string TenantKey = "tenant-a";

    [Fact]
    public async Task Evaluate_FullySatisfied_IsAvailable()
    {
        var (evaluator, _) = await CreateAsync(entitled: true);

        var result = await evaluator.EvaluateAsync(PluginId, WorkspaceKey);

        Assert.True(result.IsAvailable);
        Assert.Empty(result.UnmetFactors);
    }

    [Fact]
    public async Task Evaluate_WithoutEntitlement_IsUnavailableButStillWorkspaceEnabled()
    {
        var (evaluator, _) = await CreateAsync(entitled: false);

        var result = await evaluator.EvaluateAsync(PluginId, WorkspaceKey);

        Assert.False(result.IsAvailable);
        Assert.Contains(PluginAvailabilityFactor.Entitled, result.UnmetFactors);
        // Desired activation is preserved: the workspace stays enabled.
        Assert.DoesNotContain(PluginAvailabilityFactor.WorkspaceEnabled, result.UnmetFactors);
    }

    private static async Task<(PluginAvailabilityEvaluator Evaluator, InMemoryWorkspacePluginActivationStore Activations)> CreateAsync(bool entitled)
    {
        var now = DateTimeOffset.UtcNow;
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(PluginId, "Plugin X", "/plugins/plugin-x.dll", null, now));

        var lifecycle = new FakeHostPluginLifecycle
        {
            Plugins = [new HostPluginDescriptor(PluginId, "Plugin X", "/plugins/plugin-x.dll", null, HostPluginState.Active)]
        };

        var entitlements = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        if (entitled)
        {
            await entitlements.SetEntitledAsync(PluginId, isEntitled: true, WorkspaceKey, TenantKey);
        }

        var activations = new InMemoryWorkspacePluginActivationStore();
        await activations.SetActiveAsync(PluginId, WorkspaceKey, TenantKey, isActive: true);

        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant(TenantKey, isActive: true);
        await workspaces.UpsertAsync(TenantKey, WorkspaceKey, "Workspace A", "standard", isActive: true);

        var guard = new PluginCapabilityGuard(installations, activations);
        var evaluator = new PluginAvailabilityEvaluator(installations, lifecycle, entitlements, activations, workspaces, guard);
        return (evaluator, activations);
    }
}
