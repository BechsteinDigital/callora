using System.Net;
using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Builder;
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

        var result = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaderKinds.XForwardedProto
                | ForwardedHeaderKinds.XForwardedHost
                | ForwardedHeaderKinds.XForwardedFor,
            ForwardLimit = options.ForwardLimit <= 0 ? null : options.ForwardLimit,
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
    /// Applies forwarded-header processing when enabled; a no-op otherwise. Call this
    /// first in the middleware pipeline, before anything that reads scheme/host.
    /// </summary>
    public static IApplicationBuilder UseBackendForwardedHeaders(
        this IApplicationBuilder app,
        BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        if (options.ForwardedHeaders.Enabled)
        {
            app.UseForwardedHeaders(Build(options.ForwardedHeaders));
        }

        return app;
    }
}
