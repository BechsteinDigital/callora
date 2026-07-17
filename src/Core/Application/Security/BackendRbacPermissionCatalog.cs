using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Projects role-function-action RBAC config to permission-key sets.
/// </summary>
[CalloraInternal("RBAC config projection — not a plugin contract (REV2 §7.2)")]
public static class BackendRbacPermissionCatalog
{
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Build(BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var byRole = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        foreach (var role in options.RbacRoles)
        {
            if (string.IsNullOrWhiteSpace(role.Role))
            {
                throw new InvalidOperationException("RBAC role name must not be empty.");
            }

            var permissions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var function in role.Functions)
            {
                if (string.IsNullOrWhiteSpace(function.Function))
                {
                    throw new InvalidOperationException($"RBAC function name must not be empty for role '{role.Role}'.");
                }

                foreach (var action in function.Actions)
                {
                    var normalizedAction = action.Trim().ToLowerInvariant();
                    if (!IsKnownAction(normalizedAction))
                    {
                        throw new InvalidOperationException($"RBAC action '{action}' is invalid for role '{role.Role}'.");
                    }

                    permissions.Add($"{function.Function.Trim().ToLowerInvariant()}.{normalizedAction}");
                }
            }

            byRole[role.Role.Trim()] = permissions.ToArray();
        }

        return byRole;
    }

    private static bool IsKnownAction(string action)
    {
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
