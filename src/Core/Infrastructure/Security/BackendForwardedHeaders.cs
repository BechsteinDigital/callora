using System.Net;
using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ForwardedHeaderKinds = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Translates <see cref="BackendForwardedHeadersOptions"/> into ASP.NET's
/// <see cref="ForwardedHeadersOptions"/>. Applied early in the pipeline so the app
/// observes the external scheme/host set by a trusted proxy — the prerequisite for
/// the same-origin CSRF check, <c>Secure</c> cookies and absolute redirects behind
/// a TLS terminator.
/// </summary>
[CalloraInternal("Forwarded-headers wiring — not a plugin contract")]
public static class BackendForwardedHeaders
{
    /// <summary>
    /// Builds the framework options: proto + host + for, the configured hop limit,
    /// and the trusted proxies/networks. When none are configured the loopback-only
    /// defaults are cleared so a dynamic-address upstream is trusted (safe only when
    /// the app port is not publicly reachable — the documented topology).
    /// </summary>
    public static ForwardedHeadersOptions Build(BackendForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // X-Forwarded-For rewrites Connection.RemoteIpAddress, which is the identity
        // the rate limiter partitions on. Honour it only when trust is explicit
        // (#106): with empty trust lists ASP.NET applies the header from *any* peer,
        // so a direct client could hand itself a fresh login bucket per request.
        // Proto/host keep the immediate-upstream trust the compose topology needs.
        var trustsExplicitProxies = HasExplicitTrust(options);

        var result = new ForwardedHeadersOptions
        {
            ForwardedHeaders = trustsExplicitProxies
                ? ForwardedHeaderKinds.XForwardedProto
                    | ForwardedHeaderKinds.XForwardedHost
                    | ForwardedHeaderKinds.XForwardedFor
                : ForwardedHeaderKinds.XForwardedProto
                    | ForwardedHeaderKinds.XForwardedHost,
            // <= 0 would map to null = "unlimited hops" in ASP.NET; clamp to a single
            // proxy instead so a misconfiguration tightens rather than removes the limit.
            ForwardLimit = options.ForwardLimit <= 0 ? 1 : options.ForwardLimit,
        };

        // Drop the framework's loopback-only defaults so trust is exactly what we
        // configure below. With nothing configured the lists stay empty, which trusts
        // the immediate upstream — necessary for a compose-internal proxy at a dynamic
        // address, and safe only because the app's port is private behind that proxy.
        result.KnownIPNetworks.Clear();
        result.KnownProxies.Clear();

        foreach (var proxy in options.KnownProxies)
        {
            if (IPAddress.TryParse(proxy?.Trim(), out var address))
            {
                result.KnownProxies.Add(address);
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (IPNetwork.TryParse(network?.Trim() ?? string.Empty, out var parsed))
            {
                result.KnownIPNetworks.Add(parsed);
            }
        }

        return result;
    }

    /// <summary>
    /// Whether the deployment names its trusted proxies explicitly — the condition
    /// under which <c>X-Forwarded-For</c> may rewrite the connection's remote
    /// address. Without it the header is accepted from any peer and is therefore
    /// spoofable.
    /// </summary>
    public static bool HasExplicitTrust(BackendForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.KnownProxies.Any(static x => !string.IsNullOrWhiteSpace(x)) ||
               options.KnownNetworks.Any(static x => !string.IsNullOrWhiteSpace(x));
    }

    /// <summary>
    /// Applies forwarded-header processing when enabled; a no-op otherwise. Call this
    /// first in the middleware pipeline, before anything that reads scheme/host.
    /// </summary>
    public static IApplicationBuilder UseBackendForwardedHeaders(
        this IApplicationBuilder app,
        BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ForwardedHeaders.Enabled)
        {
            return app;
        }

        if (!HasExplicitTrust(options.ForwardedHeaders))
        {
            app.ApplicationServices
                .GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(BackendForwardedHeaders))
                .LogWarning(
                    "Forwarded headers are enabled without KnownProxies/KnownNetworks. " +
                    "X-Forwarded-For stays unprocessed, so per-client rate limits partition " +
                    "on the proxy address. Configure BackendHost:ForwardedHeaders:KnownNetworks " +
                    "to restore per-client limits.");
        }

        app.UseForwardedHeaders(Build(options.ForwardedHeaders));
        return app;
    }
}
