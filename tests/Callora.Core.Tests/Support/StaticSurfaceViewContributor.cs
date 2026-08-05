using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>A surface view contributor over a fixed view list.</summary>
public sealed class StaticSurfaceViewContributor(
    string pluginId,
    IReadOnlyList<HostSurfaceViewRegistration> views)
    : IHostSurfaceViewContributor
{
    public string PluginId { get; } = pluginId;

    public IReadOnlyList<HostSurfaceViewRegistration> Views { get; } = views;
}
