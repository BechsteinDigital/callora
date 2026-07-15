using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Endpoint-level authorization helpers for permission-gated backend routes.
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// Requires one permission key, unless the caller is a super administrator.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionKey)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        builder.RequireAuthorization(policy =>
            policy.RequireAssertion(context => HasPermission(context.User, permissionKey)));

        return builder;
    }

    /// <summary>
    /// Returns whether the current user has the provided permission key.
    /// </summary>
    public static bool UserHasPermission(ClaimsPrincipal user, string permissionKey)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        return HasPermission(user, permissionKey);
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionKey)
    {
        if (user.IsInRole(BackendRoles.SuperAdmin))
        {
            return true;
        }

        return HasPermissionClaim(user, BackendClaimTypes.Permission, permissionKey) ||
               HasPermissionClaim(user, BackendClaimTypes.Scope, permissionKey);
    }

    private static bool HasPermissionClaim(ClaimsPrincipal user, string claimType, string permissionKey)
    {
        foreach (var claim in user.FindAll(claimType))
        {
            if (string.Equals(claim.Value, "*", StringComparison.Ordinal) ||
                string.Equals(claim.Value, permissionKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
