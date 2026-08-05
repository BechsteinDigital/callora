namespace Callora.Core.Application.Surfaces;

/// <summary>
/// One navigation entry the host decided this caller may see (#125 block C): already
/// filtered by surface, claims and plugin availability, and already ordered.
/// </summary>
/// <param name="Id">Entry id, unique within its plugin.</param>
/// <param name="PluginId">Plugin that contributed the entry.</param>
/// <param name="Label">Text the theme displays.</param>
/// <param name="To">Target relative to the surface root.</param>
/// <param name="Icon">Optional icon key.</param>
/// <param name="Order">Ascending order.</param>
public sealed record SurfaceNavigationEntry(
    string Id,
    string PluginId,
    string Label,
    string To,
    string? Icon,
    int Order);
