using System.Security.Claims;
using Callora.Host.Backend.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;

namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Projects RBAC role and permission claims from configured user-role assignments.
/// </summary>
public sealed class BackendClaimsTransformation(IBackendRbacStore rbacStore) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        var effectiveRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
            effectiveRoles.Add(claim.Value);
        foreach (var claim in principal.FindAll("role"))
            effectiveRoles.Add(claim.Value);

        var userId = ResolveUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var assignedRole = await rbacStore.GetUserRoleAsync(userId).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(assignedRole) && effectiveRoles.Add(assignedRole))
                identity.AddClaim(new Claim(ClaimTypes.Role, assignedRole));
        }

        foreach (var role in effectiveRoles)
        {
            var permissions = await rbacStore.GetRolePermissionsAsync(role).ConfigureAwait(false);
            if (permissions is null)
                continue;

            foreach (var permission in permissions)
            {
                if (!principal.HasClaim(BackendClaimTypes.Permission, permission))
                    identity.AddClaim(new Claim(BackendClaimTypes.Permission, permission));
            }
        }

        return principal;
    }

    private static string? ResolveUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("sub") ??
               principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
               principal.FindFirstValue(ClaimTypes.Name);
    }
}
