using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Controller-action authorization requiring one permission key. Mirrors the
/// minimal-API <c>RequirePermission</c> filter exactly: super admins pass
/// unconditionally, otherwise a <c>"*"</c> or matching permission/scope claim is
/// required. Apply alongside <c>[Authorize]</c>, which enforces authentication.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CalloraPermissionAttribute(string permissionKey) : Attribute, IAuthorizationFilter
{
    public string PermissionKey { get; } = permissionKey;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!EndpointAuthorizationExtensions.UserHasPermission(user, PermissionKey))
            context.Result = new ForbidResult();
    }
}
