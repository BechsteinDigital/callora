using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>A surface API contributor over a fixed route list.</summary>
public sealed class StaticSurfaceApiContributor(
    string pluginId,
    IReadOnlyList<HostSurfaceApiRouteRegistration> routes)
    : IHostSurfaceApiContributor
{
    public string PluginId { get; } = pluginId;

    public IReadOnlyList<HostSurfaceApiRouteRegistration> Routes { get; } = routes;
}
