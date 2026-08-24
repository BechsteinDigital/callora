using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The platform verdict answers "may this plugin do any work on this host at all" — the
/// question platform-wide jobs, platform-wide events and plugin-wide routes actually ask.
/// It is not workspace availability with the workspace part waived.
/// </summary>
public sealed class PlatformVerdictAsksADifferentQuestionTests
{
    private const string PluginId = "plugin-x";
    private const string TenantKey = "tenant-a";
    private const string WorkspaceKey = "workspace-a";

    [Fact]
    public async Task An_installed_healthy_entitled_plugin_may_work()
    {
        var evaluator = await CreateAsync(tenantEntitled: true);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.True(result.IsAvailable);
        Assert.Empty(result.UnmetFactors);
    }

    [Fact]
    public async Task A_marketplace_grant_written_as_a_tenant_row_is_found()
    {
        // The decision this test exists for. MarketplaceEntitlementApplier writes a TENANT
        // row for a workspace-less grant, never a platform row. Asking with tenantKey: null
        // would skip that row by the store's precedence and fall through to
        // DefaultPluginEntitlement — so with an explicit-grants deployment (PLAT-253 sets
        // the default false for cloud/marketplace) a paid plugin would sit idle. The
        // platform verdict therefore asks on the default tenant.
        var evaluator = await CreateAsync(tenantEntitled: true, defaultEntitlement: false);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task A_revoked_plugin_may_not_work()
    {
        var evaluator = await CreateAsync(tenantEntitled: false, defaultEntitlement: false);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.False(result.IsAvailable);
        Assert.Contains(PluginAvailabilityFactor.Entitled, result.UnmetFactors);
    }

    [Fact]
    public async Task A_plugin_activated_in_no_workspace_may_still_work_platform_wide()
    {
        // The clearest statement that this is a different question: workspace activation
        // is not a precondition of platform-wide work, so the verdict must not consider it.
        var evaluator = await CreateAsync(tenantEntitled: true, activateInWorkspace: false);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.True(result.IsAvailable);
        Assert.DoesNotContain(PluginAvailabilityFactor.WorkspaceEnabled, result.UnmetFactors);
    }

    [Fact]
    public async Task An_uninstalled_plugin_may_not_work()
    {
        var evaluator = await CreateAsync(tenantEntitled: true, installed: false);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.False(result.IsAvailable);
        Assert.Contains(PluginAvailabilityFactor.BundledOrInstalled, result.UnmetFactors);
    }

    [Fact]
    public async Task A_faulted_plugin_may_not_work()
    {
        var evaluator = await CreateAsync(tenantEntitled: true, healthy: false);

        var result = await evaluator.EvaluatePlatformAsync(PluginId);

        Assert.False(result.IsAvailable);
        Assert.Contains(PluginAvailabilityFactor.RuntimeHealthy, result.UnmetFactors);
    }

    private static async Task<PluginAvailabilityEvaluator> CreateAsync(
        bool tenantEntitled,
        bool defaultEntitlement = true,
        bool installed = true,
        bool healthy = true,
        bool activateInWorkspace = true)
    {
        var now = DateTimeOffset.UtcNow;
        var options = new BackendHostOptions
        {
            DefaultTenantKey = TenantKey,
            DefaultPluginEntitlement = defaultEntitlement
        };

        var installations = new InMemoryPluginInstallationRepository();
        if (installed)
        {
            await installations.AddAsync(
                PluginInstallation.CreateInstalled(PluginId, "Plugin X", "/plugins/plugin-x.dll", null, now));
        }

        var lifecycle = new FakeHostPluginLifecycle
        {
            Plugins = healthy
                ? [new HostPluginDescriptor(PluginId, "Plugin X", "/plugins/plugin-x.dll", null, HostPluginState.Active)]
                : [new HostPluginDescriptor(PluginId, "Plugin X", "/plugins/plugin-x.dll", null, HostPluginState.Faulted)]
        };

        var entitlements = new InMemoryPluginEntitlementStore(options);
        if (tenantEntitled)
        {
            // Exactly the shape the marketplace applier writes for a workspace-less grant.
            await entitlements.SetEntitledAsync(
                PluginId, isEntitled: true, workspaceKey: null, tenantKey: TenantKey, source: "marketplace");
        }

        var activations = new InMemoryWorkspacePluginActivationStore();
        if (activateInWorkspace)
        {
            await activations.SetActiveAsync(PluginId, WorkspaceKey, TenantKey, isActive: true);
        }

        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant(TenantKey, isActive: true);
        await workspaces.UpsertAsync(TenantKey, WorkspaceKey, "Workspace A", "standard", isActive: true);

        return new PluginAvailabilityEvaluator(
            installations, lifecycle, entitlements, activations, workspaces,
            new PluginCapabilityGuard(installations, activations),
            faultRegistry: null,
            hostOptions: options);
    }
}
