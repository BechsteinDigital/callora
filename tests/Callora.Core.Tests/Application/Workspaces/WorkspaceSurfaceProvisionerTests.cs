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
        var (workspaceStore, surfaceStore) = await CreateStoresAsync("https://meet.example.test");
        var service = new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore);

        var location = await service.EnsureAsync("acme", Definition());

        Assert.NotNull(location);
        Assert.Equal("/meet", location!.PublicPath);
        Assert.Equal("https://meet.example.test/meet", location.PublicUrl);
        var surface = await surfaceStore.GetAsync("acme", "videoconference");
        Assert.NotNull(surface);
        Assert.Equal(SurfaceAuthentication.Public, surface!.Authentication);
        Assert.Equal("videoconference", surface.TemplatePluginId);
    }

    [Fact]
    public async Task Ensure_PrefixedWorkspace_AppendsSurfaceSuffix()
    {
        var (workspaceStore, surfaceStore) = await CreateStoresAsync("https://example.test/acme");
        var service = new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore);

        var location = await service.EnsureAsync("acme", Definition());

        Assert.NotNull(location);
        Assert.Equal("/acme/meet", location!.PublicPath);
        Assert.Equal("https://example.test/acme/meet", location.PublicUrl);
    }

    [Fact]
    public async Task Ensure_ExistingSurface_PreservesOperatorThemeAndLocale()
    {
        var (workspaceStore, surfaceStore) = await CreateStoresAsync("https://example.test/acme");
        _ = await surfaceStore.UpsertAsync(
            "acme",
            new WorkspaceSurfaceInput(
                "videoconference",
                "Old",
                "spa",
                null,
                "example.test",
                "/old",
                SurfaceAuthentication.Public,
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
        // Das eigene SEGMENT, nicht der fertige Pfad (ADR-021). Den vollen Pfad hier zu
        // speichern verschluckte die Kette darüber: Die Fläche lag direkt unter der Wurzel statt
        // unter dem Standard-Eingang, und das Workspace-Segment fehlte in jedem Link, den das
        // Plugin ausgab.
        Assert.Equal("meet", surface.PublicPathPrefix);
        Assert.Equal("default", surface.ParentSurfaceKey);
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
            PluginSurfaceAuthentication.Public,
            "videoconference",
            "0.1.0");

    /// <summary>
    /// A workspace plus the "default" surface carrying its route — the state the
    /// real upsert leaves behind. Plugin surfaces route below that surface.
    /// </summary>
    private static async Task<(InMemoryWorkspaceManagementStore Workspaces, InMemoryWorkspaceSurfaceStore Surfaces)>
        CreateStoresAsync(string publicBaseUrl)
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

        var uri = new Uri(publicBaseUrl);
        var surfaces = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaces.UpsertAsync(
            "acme",
            new WorkspaceSurfaceInput(
                "default",
                "Acme",
                "spa",
                publicBaseUrl,
                uri.Host,
                uri.AbsolutePath.TrimEnd('/') is { Length: > 0 } path ? path : "/",
                SurfaceAuthentication.Public,
                null,
                null,
                null,
                null,
                null,
                true));

        return (store, surfaces);
    }
}
