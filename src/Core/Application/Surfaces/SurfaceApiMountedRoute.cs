using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// One surface API route the host actually serves, together with the plugin that owns
/// it (#125 block B).
/// </summary>
/// <param name="PluginId">Plugin owning the route.</param>
/// <param name="Route">The declared route.</param>
public sealed record SurfaceApiMountedRoute(string PluginId, HostSurfaceApiRouteRegistration Route);
