using Callora.Core.Application.Extensions;
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
