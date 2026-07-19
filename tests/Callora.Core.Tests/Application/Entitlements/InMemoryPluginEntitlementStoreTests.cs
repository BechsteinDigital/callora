using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Policies;
using Xunit;

namespace Callora.Core.Tests.Application.Entitlements;

public sealed class InMemoryPluginEntitlementStoreTests
{
    [Fact]
    public async Task ListAsync_EmptyStore_ReturnsNothing()
    {
        var store = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task ListAsync_SurfacesConfiguredPlatformEntitlements_WithConfigSource()
    {
        var store = new InMemoryPluginEntitlementStore(new BackendHostOptions
        {
            ActivationEntitledPluginIds = ["seed.plugin"],
        });

        var list = await store.ListAsync();

        var seeded = Assert.Single(list);
        Assert.Equal("seed.plugin", seeded.PluginId);
        Assert.Null(seeded.WorkspaceKey);
        Assert.Null(seeded.TenantKey);
        Assert.True(seeded.IsEntitled);
        Assert.Equal("config", seeded.Source);
    }

    [Fact]
    public async Task GrantThenList_ReturnsTheScopedEntitlement()
    {
        var store = new InMemoryPluginEntitlementStore(new BackendHostOptions());

        await store.SetEntitledAsync("acme.plugin", isEntitled: true, workspaceKey: "workspace-a");

        var granted = Assert.Single(await store.ListAsync());
        Assert.Equal("acme.plugin", granted.PluginId);
        Assert.Equal("workspace-a", granted.WorkspaceKey);
        Assert.True(granted.IsEntitled);
    }

    [Fact]
    public async Task GrantRecordsProvenance_DefaultManual_OrExplicitSource()
    {
        var store = new InMemoryPluginEntitlementStore(new BackendHostOptions());

        await store.SetEntitledAsync("acme.plugin", isEntitled: true, workspaceKey: "workspace-a"); // default
        await store.SetEntitledAsync("acme.plugin", isEntitled: true, tenantKey: "tenant-a", source: "marketplace");

        var list = await store.ListAsync();
        Assert.Contains(list, x => x.WorkspaceKey == "workspace-a" && x.Source == "manual");
        Assert.Contains(list, x => x.TenantKey == "tenant-a" && x.Source == "marketplace");
    }

    [Fact]
    public async Task Revoke_RemovesTheEntitlementFromTheList()
    {
        var store = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        await store.SetEntitledAsync("acme.plugin", isEntitled: true, workspaceKey: "workspace-a");

        await store.SetEntitledAsync("acme.plugin", isEntitled: false, workspaceKey: "workspace-a");

        Assert.Empty(await store.ListAsync());
        Assert.False(await store.IsEntitledAsync("acme.plugin", workspaceKey: "workspace-a"));
    }
}
