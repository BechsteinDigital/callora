using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Extensions;

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
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]));

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["template-alpha", "template-beta", "dialer", "voip"], chain);
    }

    [Fact]
    public async Task Resolve_DeduplicatesTemplatePluginsThatAreAlsoActivated()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([Template("template-alpha", "workspace.base")]),
            new StaticWorkspacePluginActivationReader(["template-alpha", "voip"]));

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["template-alpha", "voip"], chain);
    }

    [Fact]
    public async Task Resolve_WithoutTemplates_ReturnsActivePluginsOnly()
    {
        var resolver = new WorkspaceUiChainResolver(
            new StaticWorkspaceTemplateResolutionService([]),
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]));

        var chain = await resolver.ResolveAsync("workspace-a");

        Assert.Equal(["dialer", "voip"], chain);
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
