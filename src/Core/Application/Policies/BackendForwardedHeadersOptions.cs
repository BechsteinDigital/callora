namespace Callora.Core.Application.Policies;

/// <summary>
/// Forwarded-header handling for deployments behind a reverse proxy that
/// terminates TLS (the standard production topology: Caddy/Nginx in front of the
/// app). When enabled, <c>X-Forwarded-Proto</c>/<c>-Host</c>/<c>-For</c> are
/// applied so the app observes the external scheme and host instead of the
/// internal proxied connection. This is required for the same-origin CSRF check
/// (<see cref="BackendHostOptions.AllowedCsrfOrigins"/>), <c>Secure</c> cookies and
/// absolute redirects to reflect the public <c>https://</c> origin.
/// <para>
/// Leave disabled when the app faces clients directly: trusting these headers from
/// an untrusted source lets a client spoof its origin. Only enable it when a
/// trusted proxy sits in front and the app's own port is not publicly reachable.
/// </para>
/// </summary>
public sealed class BackendForwardedHeadersOptions
{
    /// <summary>Whether to apply forwarded headers. Off by default (direct-facing, fail-safe).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Trusted proxy IP addresses. Combined with <see cref="KnownNetworks"/>; when both
    /// are empty the immediate upstream is trusted (see the remarks on <see cref="KnownNetworks"/>).
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Trusted proxy networks in CIDR notation (e.g. <c>172.16.0.0/12</c>). When both this
    /// and <see cref="KnownProxies"/> are empty, the framework's loopback-only defaults are
    /// cleared so the immediate upstream is trusted — necessary when the proxy runs at a
    /// dynamic address (a compose-internal Caddy), and safe only because the app port is not
    /// published. Set explicit values to restrict trust in less isolated deployments.
    /// </summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>Maximum number of chained proxy hops to honour. Default 1 (a single front proxy).</summary>
    public int ForwardLimit { get; set; } = 1;
}
