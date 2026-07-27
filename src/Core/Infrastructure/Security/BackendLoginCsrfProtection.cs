using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Same-origin guard for the cookie-issuing login endpoints. The global
/// <see cref="BackendCsrfValidator.IsForbidden"/> middleware only checks requests that
/// already carry the auth cookie, so the login POST itself — which sets a fresh cookie —
/// is otherwise unprotected against login-CSRF / session fixation. This endpoint filter
/// closes that gap via <see cref="BackendCsrfValidator.IsForbiddenLogin"/>: a cross-origin
/// browser login is rejected, while a source-less non-browser client keeps working.
/// </summary>
[CalloraInternal("Login-CSRF endpoint filter — not a plugin contract")]
public static class BackendLoginCsrfProtection
{
    /// <summary>
    /// Rejects cross-origin browser logins with <c>403 Forbidden</c> before the handler
    /// runs. Apply to every endpoint that issues an auth cookie.
    /// </summary>
    /// <param name="builder">The login route being configured.</param>
    public static RouteHandlerBuilder RequireSameOriginLogin(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(static async (context, next) =>
        {
            var request = context.HttpContext.Request;
            var options = context.HttpContext.RequestServices.GetRequiredService<BackendHostOptions>();
            var requestOrigin = $"{request.Scheme}://{request.Host.Value}";

            if (BackendCsrfValidator.IsForbiddenLogin(
                    request.Method,
                    request.Headers.Origin,
                    request.Headers.Referer,
                    requestOrigin,
                    options.AllowedCsrfOrigins))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
    }
}
