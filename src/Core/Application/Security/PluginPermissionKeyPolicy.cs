using Callora.Core.Extensibility;
using System.Reflection;

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
    /// The function segments the host's own keys occupy. No plugin may declare inside them,
    /// whatever it calls itself.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="BackendPermissionKeys"/> rather than listed by hand. A
    /// hand-kept list is one release away from being wrong, and wrong here means a plugin
    /// holding a host permission.
    /// </remarks>
    public static IReadOnlySet<string> ReservedFunctions { get; } = typeof(BackendPermissionKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => field.GetRawConstantValue() as string ?? string.Empty)
        .Where(value => value.Contains('.', StringComparison.Ordinal))
        .Select(value => value[..value.IndexOf('.', StringComparison.Ordinal)])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        var owner = pluginId.Trim();

        // Checked BEFORE the namespace rule, because the namespace rule cannot see this:
        // pluginId is only validated for being non-empty, so a plugin can call itself
        // "user" — and then "user.delete" is genuinely inside its own namespace. An
        // operator reading its declared permissions would see what looks like the plugin's
        // key and grant the host's. Choosing the namespace defeated the rule that guards it.
        if (ReservedFunctions.Contains(owner))
        {
            reason = $"'{owner}' is a host permission namespace; a plugin may not declare keys in it.";
            return false;
        }

        // Authorization compares permission claims with StringComparison.Ordinal and
        // BackendRbacPermissionCatalog emits lower case. A key declared with capitals would
        // pass here and then never match anything — this issue's own failure mode, one
        // layer up: it looks right and answers 403 forever. Refused rather than silently
        // normalised, because the route still demands the string the author wrote.
        if (!string.Equals(key, key.ToLowerInvariant(), StringComparison.Ordinal))
        {
            reason = $"'{key}' must be lower case; permission keys are compared exactly.";
            return false;
        }

        var prefix = owner + ".";

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
