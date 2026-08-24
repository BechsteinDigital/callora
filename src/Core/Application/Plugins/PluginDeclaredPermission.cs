namespace Callora.Core.Application.Plugins;

/// <summary>
/// One permission key a plugin declares in its manifest, so an operator can grant what its
/// routes require.
/// </summary>
/// <param name="Key">
/// The key, inside the plugin's own namespace and ending in a known action — see
/// <c>PluginPermissionKeyPolicy</c> for why both are enforced.
/// </param>
/// <param name="Description">
/// What granting it allows, shown to the operator doing the granting. Optional, and its
/// absence is the difference between an informed grant and a guessed one.
/// </param>
public sealed record PluginDeclaredPermission(string Key, string? Description = null);
