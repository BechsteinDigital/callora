using Callora.Core.Api;
using Callora.Core.Application.Security;
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

        return builder.RequirePermissions(permissionKey);
    }

    /// <summary>
    /// Requires at least one of the permission keys, unless the caller is a
    /// super administrator. Used where a capability is reachable through two
    /// legitimate grants — e.g. workspace membership administration, held
    /// either by a platform workspace manager or by a workspace administrator.
    /// </summary>
    public static TBuilder RequireAnyPermission<TBuilder>(this TBuilder builder, params string[] permissionKeys)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permissionKeys);
        if (permissionKeys.Length == 0)
        {
            throw new ArgumentException("At least one permission key is required.", nameof(permissionKeys));
        }

        foreach (var permissionKey in permissionKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        }

        return builder.RequirePermissions(permissionKeys);
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

    /// <summary>
    /// Authentication stays a policy; the permission becomes an endpoint filter.
    /// </summary>
    /// <remarks>
    /// A policy can only say yes or no — <c>RequireAssertion</c> yields a bare 403 with no
    /// body, so an operator debugging a role grant had to bisect the 37-key catalogue by
    /// hand. A filter can answer, and it answers with the key that was missing. The
    /// decision itself is unchanged and still comes from <see cref="UserHasPermission"/>.
    /// </remarks>
    private static TBuilder RequirePermissions<TBuilder>(this TBuilder builder, params string[] permissionKeys)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var user = invocationContext.HttpContext.User;
            var missing = permissionKeys.FirstOrDefault(key => !HasPermission(user, key));
            if (permissionKeys.Any(key => HasPermission(user, key)))
            {
                return await next(invocationContext).ConfigureAwait(false);
            }

            return Results.Problem(
                title: "Forbidden",
                detail: $"The permission '{missing}' is required.",
                statusCode: StatusCodes.Status403Forbidden,
                type: ApiProblems.TypeBaseUri + "forbidden",
                extensions: new Dictionary<string, object?> { ["missingPermission"] = missing });
        });

        return builder;
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
