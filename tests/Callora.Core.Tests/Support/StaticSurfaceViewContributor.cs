using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>A surface view contributor over a fixed view and navigation list.</summary>
public sealed class StaticSurfaceViewContributor(
    string pluginId,
    IReadOnlyList<HostSurfaceViewRegistration> views,
    IReadOnlyList<HostSurfaceNavigationItem>? navigationItems = null)
    : IHostSurfaceViewContributor
{
    public string PluginId { get; } = pluginId;

    public IReadOnlyList<HostSurfaceViewRegistration> Views { get; } = views;

    public IReadOnlyList<HostSurfaceNavigationItem> NavigationItems { get; } = navigationItems ?? [];
}
