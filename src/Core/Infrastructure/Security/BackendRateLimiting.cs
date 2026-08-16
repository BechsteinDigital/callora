using Callora.Core.Application.Policies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Policy-based rate limiting: a strict fixed window for login endpoints and
/// a generous per-client window for the general API surface.
/// </summary>
public static class BackendRateLimiting
{
    public const string AuthPolicy = "auth";
    public const string ApiPolicy = "api";

    /// <summary>
    /// Für die Senke, die Fehler aus fremden Browsern annimmt (#294). Eng, und aus einem anderen
    /// Grund als beim Login: Dort begrenzt das Fenster einen Angreifer, hier eine kaputte Seite,
    /// die in einer Schleife meldet — ein Bug im Browser eines Besuchers darf kein Logziel füllen.
    /// </summary>
    public const string ClientErrorPolicy = "client-errors";

    public static IServiceCollection AddBackendRateLimiting(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };

            limiter.AddPolicy(AuthPolicy, httpContext =>
                CreateClientWindow(httpContext, options.RateLimitAuthPerMinute));
            limiter.AddPolicy(ApiPolicy, httpContext =>
                CreateClientWindow(httpContext, options.RateLimitApiPerMinute));
            limiter.AddPolicy(ClientErrorPolicy, httpContext =>
                CreateClientWindow(httpContext, options.RateLimitClientErrorsPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> CreateClientWindow(HttpContext httpContext, int permitPerMinute)
    {
        if (permitPerMinute <= 0)
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    }

    /// <summary>
    /// Partitions per client on the connection's remote address.
    /// <para>
    /// Deliberately never reads <c>X-Forwarded-For</c> directly (#106): a raw header
    /// is attacker-controlled, so rotating it would hand out a fresh login bucket per
    /// request. Behind a proxy the address arrives here only through
    /// <c>UseForwardedHeaders</c>, which applies the header solely from a configured
    /// trusted proxy — see <see cref="BackendForwardedHeaders"/>.
    /// </para>
    /// </summary>
    public static string ResolveClientKey(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
