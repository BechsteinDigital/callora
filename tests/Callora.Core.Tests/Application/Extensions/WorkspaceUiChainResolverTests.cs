using Callora.Core.Application.Extensions;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Extensions;

public sealed class WorkspaceUiChainResolverTests
{
    [Fact]
    public async Task Resolve_OrdersTemplatePluginsBeforeActivePlugins()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService(
            [
                Template("template-alpha", "workspace.base"),
                Template("template-alpha", "workspace.dashboard"),
                Template("template-beta", "workspace.sidebar")
            ]),
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]),
            new StaticPluginAvailabilityEvaluator());

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["template-alpha", "template-beta", "dialer", "voip"], chain);
    }

    [Fact]
    public async Task Resolve_DeduplicatesTemplatePluginsThatAreAlsoActivated()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["template-alpha", "voip"]),
            new StaticPluginAvailabilityEvaluator());

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["template-alpha", "voip"], chain);
    }

    [Fact]
    public async Task Resolve_WithoutTemplates_ReturnsActivePluginsOnly()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([]),
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]),
            new StaticPluginAvailabilityEvaluator());

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["dialer", "voip"], chain);
    }

    [Fact]
    public async Task Resolve_ExcludesActivePluginThatIsNotEffectivelyAvailable()
    {
        // 'voip' is activated but not effectively available (e.g. lapsed
        // entitlement) → it drops from the UI chain while 'dialer' remains.
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([]),
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]),
            new StaticPluginAvailabilityEvaluator("voip"));

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["dialer"], chain);
    }

    [Fact]
    public async Task Resolve_ForSurface_PrependsAssignedSurfaceTemplate()
    {
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaceStore.UpsertAsync(
            "workspace-a",
            new WorkspaceSurfaceInput(
                SurfaceKey: "videoconference",
                DisplayName: "Video conference",
                SurfaceType: "videoconference",
                PublicBaseUrl: null,
                PublicHost: null,
                PublicPathPrefix: "/meet",
                Authentication: SurfaceAuthentication.Public,
                Locale: "de",
                TemplatePluginId: "videoconference",
                TemplateVersion: "1.0.0",
                ThemePluginId: null,
                ThemeVersion: null,
                IsActive: true));
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService(
                [Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["videoconference", "dialer"]),
            new StaticPluginAvailabilityEvaluator(),
            surfaceStore);

        var chain = await resolver.ResolveAsync("workspace-a", "videoconference");

        // Die App und ihr Theme — sonst nichts. `dialer` ist im Workspace aktiv, hat auf einer
        // Fläche, die der Videokonferenz gehört, aber nichts zu rendern: Eine Anwendung, in die
        // sich jede andere hineinrendert, ist keine (ADR-022).
        Assert.Equal(["videoconference", "template-alpha"], chain);
    }

    [Fact]
    public async Task Resolve_ForUnknownSurface_FallsBackToWorkspaceChain()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService(
                [Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["dialer"]),
            new StaticPluginAvailabilityEvaluator(),
            new InMemoryWorkspaceSurfaceStore());

        var chain = await resolver.ResolveAsync("workspace-a", "missing");

        Assert.Equal(["template-alpha", "dialer"], chain);
    }

    [Fact]
    public async Task Resolve_ForRender_DropsActivePluginAbsentFromPublishedLayout()
    {
        var resolver = ResolverWithLayout("communication.phone");

        var chain = await resolver.ResolveAsync("workspace-a", "content");

        // Eine Inhaltsfläche zeigt, was ihr Layout verlangt: `communication` steht darin,
        // `dialer` nicht — obwohl beide im Workspace aktiv sind.
        Assert.Equal(["template-alpha", "communication"], chain);
    }

    [Fact]
    public async Task Resolve_ForCatalog_KeepsActivePluginAbsentFromPublishedLayout()
    {
        var resolver = ResolverWithLayout("communication.phone");

        var chain = await resolver.ResolveAsync(
            "workspace-a", "content", WorkspaceUiChainPurpose.Catalog);

        // Der Editor braucht `dialer`, um dessen Blöcke überhaupt anbieten zu können. Käme hier
        // dieselbe Kürzung wie beim Rendern, könnte kein Block je als erster in eine Fläche
        // gelangen: die Palette bliebe leer, weil das Layout leer ist, und das Layout bliebe
        // leer, weil die Palette leer ist.
        Assert.Equal(["template-alpha", "communication", "dialer"], chain);
    }

    [Fact]
    public async Task Resolve_ForCatalog_OnAppOwnedSurface_StillReturnsOnlyTheApp()
    {
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaceStore.UpsertAsync(
            "workspace-a",
            new WorkspaceSurfaceInput(
                SurfaceKey: "videoconference",
                DisplayName: "Video conference",
                SurfaceType: "videoconference",
                PublicBaseUrl: null,
                PublicHost: null,
                PublicPathPrefix: "/meet",
                Authentication: SurfaceAuthentication.Public,
                Locale: "de",
                TemplatePluginId: "videoconference",
                TemplateVersion: "1.0.0",
                ThemePluginId: null,
                ThemeVersion: null,
                IsActive: true));
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["videoconference", "communication"]),
            new StaticPluginAvailabilityEvaluator(),
            surfaceStore,
            new StaticSurfaceLayoutSource(null));

        var chain = await resolver.ResolveAsync(
            "workspace-a", "videoconference", WorkspaceUiChainPurpose.Catalog);

        // Der Katalog hebt nur die Layout-Bedingung auf, nicht die Flächenzuordnung: In einen
        // Konferenzraum baut niemand Telefon-Blöcke, auch nicht im Editor.
        Assert.Equal(["videoconference", "template-alpha"], chain);
    }

    private static WorkspaceUiChainResolver ResolverWithLayout(params string[] blockIds) =>
        new(
            new StaticWorkspaceTemplateResolutionService([Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["communication", "dialer"]),
            new StaticPluginAvailabilityEvaluator(),
            new InMemoryWorkspaceSurfaceStore(),
            new StaticSurfaceLayoutSource(
                new SurfaceLayoutDocument(
                    "content",
                    1,
                    [
                        new SurfaceLayoutSection(
                            "single",
                            0,
                            [.. blockIds.Select((id, index) => new SurfaceLayoutBlock(id, "main", index))])
                    ])));

    private sealed class StaticSurfaceLayoutSource(SurfaceLayoutDocument? published) : ISurfaceLayoutSource
    {
        public Task<SurfaceLayoutDocument?> GetPublishedAsync(
            string workspaceKey,
            string surfaceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(published);

        public Task<IReadOnlySet<string>> ListPublishedSurfaceKeysAsync(
            string workspaceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(
                published is null ? new HashSet<string>() : new HashSet<string> { published.Key });

        public Task<SurfaceLayoutDocument?> GetDraftAsync(
            string layoutKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(published);
    }

    private static WorkspaceTemplateEffectiveSnapshot Template(string pluginId, string templateKey) =>
        new(
            TenantKey: "default",
            WorkspaceKey: "workspace-a",
            TemplateKey: templateKey,
            Surface: "workspace",
            PluginId: pluginId,
            Version: "1.0.0",
            DisplayName: templateKey,
            TemplatePath: $"/themes/{templateKey}.json",
            ParentTemplateKey: null,
            Scope: "workspace",
            Source: "workspace-assigned",
            Priority: 100);
}
