namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// The connection configuration of a <see cref="SipAccount"/> — grouped as one value object
/// (Introduce Parameter Object) because host/port/transport/auth form one cohesive clump. The
/// authentication is a <see cref="SipAuthentication"/> so only the fields the chosen method needs
/// are present. Secrets are never held here, only references into the secret store.
/// <para>
/// Whether the connection registers is derived from the auth type, not the mode: every connection
/// registers <em>except</em> the registration-less IP-authenticated trunk (<see cref="SipAccountMode.Trunk"/>
/// + <see cref="IpAuthentication"/>). A registering connection carries a registration expiry; the
/// IP-authenticated trunk carries none. A credentialed (digest / mutual-TLS) trunk registers like a
/// registering account but with trunk inbound behaviour — an optional <see cref="OutboundProxy"/> and
/// an <see cref="InboundNumbers"/> DID whitelist.
/// </para>
/// </summary>
public sealed record SipConnection
{
    /// <summary>Creates and validates a connection configuration.</summary>
    /// <param name="outboundProxy">Optional signalling proxy to route through instead of the host.</param>
    /// <param name="inboundNumbers">
    /// Optional DID whitelist for a trunk; blank entries are dropped, an empty list means "accept all
    /// numbers on the domain" (the SDK trunk default). Copied defensively so the record stays immutable.
    /// </param>
    public SipConnection(
        string host,
        int port,
        SipTransport transport,
        SipAccountMode mode,
        SipAuthentication authentication,
        int? registrationExpirySeconds,
        string? outboundProxy = null,
        IReadOnlyList<string>? inboundNumbers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(authentication);

        if (mode == SipAccountMode.Register && authentication.Method == SipAuthMethod.IpAuthenticated)
        {
            throw new ArgumentException(
                "A registering connection needs an identity and cannot use IP authentication.",
                nameof(authentication));
        }

        // A connection registers unless it is the registration-less IP-authenticated trunk.
        var registers = !(mode == SipAccountMode.Trunk && authentication.Method == SipAuthMethod.IpAuthenticated);
        if (registers)
        {
            if (registrationExpirySeconds is not { } expiry)
            {
                throw new ArgumentException(
                    "A registering connection requires a registration expiry.",
                    nameof(registrationExpirySeconds));
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(expiry, 1);
        }
        else if (registrationExpirySeconds is not null)
        {
            throw new ArgumentException(
                "A registration-less IP-authenticated trunk must not carry a registration expiry.",
                nameof(registrationExpirySeconds));
        }

        Host = host;
        Port = port;
        Transport = transport;
        Mode = mode;
        Authentication = authentication;
        RegistrationExpirySeconds = registrationExpirySeconds;
        OutboundProxy = string.IsNullOrWhiteSpace(outboundProxy) ? null : outboundProxy;
        InboundNumbers = NormalizeInboundNumbers(inboundNumbers);
    }

    /// <summary>SIP registrar/trunk host.</summary>
    public string Host { get; }

    /// <summary>SIP signalling port (1–65535).</summary>
    public int Port { get; }

    /// <summary>Signalling transport.</summary>
    public SipTransport Transport { get; }

    /// <summary>Register vs. trunk connection mode.</summary>
    public SipAccountMode Mode { get; }

    /// <summary>How the connection authenticates (digest / IP / mutual-TLS).</summary>
    public SipAuthentication Authentication { get; }

    /// <summary>
    /// Requested registration expiry in seconds (≥ 1); <see langword="null"/> only for the
    /// registration-less IP-authenticated trunk.
    /// </summary>
    public int? RegistrationExpirySeconds { get; }

    /// <summary>Optional outbound signalling proxy; <see langword="null"/> to resolve the host directly.</summary>
    public string? OutboundProxy { get; }

    /// <summary>
    /// Inbound number (DID) whitelist for a trunk; empty means "accept all numbers on the domain".
    /// Never <see langword="null"/> and never contains blank entries.
    /// </summary>
    public IReadOnlyList<string> InboundNumbers { get; }

    private static IReadOnlyList<string> NormalizeInboundNumbers(IReadOnlyList<string>? inboundNumbers)
    {
        if (inboundNumbers is null || inboundNumbers.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>(inboundNumbers.Count);
        foreach (var number in inboundNumbers)
        {
            if (!string.IsNullOrWhiteSpace(number))
            {
                normalized.Add(number.Trim());
            }
        }

        return normalized;
    }
}
