using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Contracts;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Workspaces;

public sealed class WorkspaceSurfaceProvisionerTests
{
    [Fact]
    public async Task Ensure_RootWorkspace_CreatesPluginOwnedSurfaceAtSuffix()
    {
        var workspaceStore = await CreateWorkspaceStoreAsync("https://meet.example.test");
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        var service = new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore);

        var location = await service.EnsureAsync("acme", Definition());

        Assert.NotNull(location);
        Assert.Equal("/meet", location!.PublicPath);
        Assert.Equal("https://meet.example.test/meet", location.PublicUrl);
        var surface = await surfaceStore.GetAsync("acme", "videoconference");
        Assert.NotNull(surface);
        Assert.Equal(SurfaceAccessMode.Mixed, surface!.AccessMode);
        Assert.Equal("videoconference", surface.TemplatePluginId);
    }

    [Fact]
    public async Task Ensure_PrefixedWorkspace_AppendsSurfaceSuffix()
    {
        var workspaceStore = await CreateWorkspaceStoreAsync("https://example.test/acme");
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        var service = new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore);

        var location = await service.EnsureAsync("acme", Definition());

        Assert.NotNull(location);
        Assert.Equal("/acme/meet", location!.PublicPath);
        Assert.Equal("https://example.test/acme/meet", location.PublicUrl);
    }

    [Fact]
    public async Task Ensure_ExistingSurface_PreservesOperatorThemeAndLocale()
    {
        var workspaceStore = await CreateWorkspaceStoreAsync("https://example.test/acme");
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaceStore.UpsertAsync(
            "acme",
            new WorkspaceSurfaceInput(
                "videoconference",
                "Old",
                "spa",
                null,
                "example.test",
                "/old",
                SurfaceAccessMode.Public,
                "en",
                "old-template",
                "1.0.0",
                "customer-theme",
                "2.0.0",
                true));
        var service = new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore);

        _ = await service.EnsureAsync("acme", Definition());

        var surface = await surfaceStore.GetAsync("acme", "videoconference");
        Assert.Equal("en", surface!.Locale);
        Assert.Equal("customer-theme", surface.ThemePluginId);
        Assert.Equal("2.0.0", surface.ThemeVersion);
        Assert.Equal("videoconference", surface.TemplatePluginId);
        Assert.Equal("/acme/meet", surface.PublicPathPrefix);
    }

    [Fact]
    public async Task Ensure_UnknownWorkspace_ReturnsNull()
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        var service = new WorkspaceSurfaceProvisioner(
            workspaceStore,
            new InMemoryWorkspaceSurfaceStore());

        var location = await service.EnsureAsync("missing", Definition());

        Assert.Null(location);
    }

    private static PluginSurfaceDefinition Definition() =>
        new(
            "videoconference",
            "Video Conference",
            "videoconference",
            "/meet",
            PluginSurfaceAccessMode.Mixed,
            "videoconference",
            "0.1.0");

    private static async Task<InMemoryWorkspaceManagementStore> CreateWorkspaceStoreAsync(
        string publicBaseUrl)
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync(
            "tenant-a",
            "acme",
            "Acme",
            "standard",
            isActive: true,
            publicBaseUrl);
        return store;
    }
}
