using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Wires the <see cref="BackendCsrfValidator"/> as middleware. Register it after
/// authentication and before authorization so a forged state change is rejected
/// before it reaches any endpoint.
/// </summary>
[CalloraInternal("CSRF middleware wiring — not a plugin contract")]
public static class BackendCsrfProtectionExtensions
{
    /// <summary>
    /// Rejects cookie-authenticated, cross-origin state-changing requests with
    /// <c>403 Forbidden</c>. Header-authenticated requests (Bearer/API key) carry
    /// no auth cookie and pass through.
    /// </summary>
    public static IApplicationBuilder UseBackendCsrfGuard(
        this IApplicationBuilder app,
        BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            var hasAuthCookie = request.Cookies.ContainsKey(
                BackendAuthCookieService.ResolveCookieName(options));
            var requestOrigin = $"{request.Scheme}://{request.Host.Value}";
            if (BackendCsrfValidator.IsForbidden(
                    request.Method,
                    hasAuthCookie,
                    request.Headers.Origin,
                    request.Headers.Referer,
                    requestOrigin,
                    options.AllowedCsrfOrigins))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next();
        });
    }
}
