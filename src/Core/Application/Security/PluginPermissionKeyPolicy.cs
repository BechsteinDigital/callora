using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Decides whether a plugin may declare a given permission key.
/// </summary>
/// <remarks>
/// <para>
/// Plugins declare the keys their routes require, because <c>CalloraRouteAttribute.Permission</c>
/// lets them demand a key and nothing could supply one — a purchased plugin arrived at a
/// customer permanently answering 403, with no way to grant what it asked for.
/// </para>
/// <para>
/// Declaration is self-service, which is exactly why it needs a boundary. Without one a
/// plugin could declare <c>user.delete</c> and have an operator grant it in good faith,
/// believing it to be the plugin's own. So a key is declarable only inside the declaring
/// plugin's namespace, and only if it ends in an action the host already knows — keys are
/// granted through role-function-action configuration, and one that cannot be expressed
/// there would move the dead end rather than remove it.
/// </para>
/// </remarks>
[CalloraInternal("Declaration boundary — enforcement, not a plugin contract (REV2 §7.2)")]
public static class PluginPermissionKeyPolicy
{
    /// <summary>
    /// Whether <paramref name="permissionKey"/> may be declared by <paramref name="pluginId"/>,
    /// with the reason when it may not.
    /// </summary>
    public static bool IsDeclarable(string pluginId, string permissionKey, out string reason)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            reason = "The plugin id is required to decide which keys it may declare.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            reason = "A permission key must not be empty.";
            return false;
        }

        var key = permissionKey.Trim();
        var prefix = pluginId.Trim() + ".";

        // The separator is part of the comparison on purpose: "communications.read" starts
        // with "communication" as a string but belongs to a different plugin.
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"'{key}' is outside the '{pluginId}' namespace; a plugin may only declare keys beginning with '{prefix}'.";
            return false;
        }

        var lastSeparator = key.LastIndexOf('.');
        var action = key[(lastSeparator + 1)..];
        if (!BackendPermissionActions.All.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"'{key}' does not end in a known action ({string.Join(", ", BackendPermissionActions.All)}).";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
