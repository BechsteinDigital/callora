using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Controller-action authorization requiring one permission key. Mirrors the
/// minimal-API <c>RequirePermission</c> filter exactly — same decision via
/// <see cref="EndpointAuthorizationExtensions.UserHasPermission"/>, and the same refusal:
/// a problem document naming the key that was missing. Apply alongside <c>[Authorize]</c>,
/// which enforces authentication.
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
        {
            // A ForbidResult answers 403 with no body, which left this path saying less than
            // the two it claims to mirror. Same shape as theirs now, missing key included.
            context.Result = new ObjectResult(new ProblemDetails
            {
                Type = Callora.Core.Api.ApiProblems.TypeBaseUri + "forbidden",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = $"The permission '{PermissionKey}' is required.",
                Extensions = { ["missingPermission"] = PermissionKey }
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
                ContentTypes = { "application/problem+json" }
            };
        }
    }
}
