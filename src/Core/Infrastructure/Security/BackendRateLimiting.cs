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
    /// Partitions per client: the first forwarded address behind the frontdoor,
    /// otherwise the remote address.
    /// </summary>
    public static string ResolveClientKey(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (first.Length > 0)
            {
                return first;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
