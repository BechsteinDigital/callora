using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Validates permission keys following &lt;function&gt;.&lt;action&gt; schema.
/// </summary>
[CalloraInternal("Permission-key validation — not a plugin contract (REV2 §7.2)")]
public static class BackendPermissionKeyValidator
{
    public static bool IsValid(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        var segments = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        var function = segments[0];
        var action = segments[1];
        if (string.IsNullOrWhiteSpace(function) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        foreach (var knownAction in BackendPermissionActions.All)
        {
            if (string.Equals(knownAction, action, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
