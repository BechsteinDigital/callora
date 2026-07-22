namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// The connection configuration of a <see cref="SipAccount"/> — grouped as one value object
/// (Introduce Parameter Object) because host/port/transport/auth form one cohesive clump. The
/// authentication is a <see cref="SipAuthentication"/> so only the fields the chosen method needs
/// are present: an IP-authenticated trunk carries no credentials and no registration expiry, while
/// a registering account does. Secrets are never held here, only references into the secret store.
/// </summary>
public sealed record SipConnection
{
    /// <summary>Creates and validates a connection configuration.</summary>
    public SipConnection(
        string host,
        int port,
        SipTransport transport,
        SipAccountMode mode,
        SipAuthentication authentication,
        int? registrationExpirySeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(authentication);

        if (mode == SipAccountMode.Register)
        {
            if (authentication.Method == SipAuthMethod.IpAuthenticated)
            {
                throw new ArgumentException(
                    "A registering connection needs an identity and cannot use IP authentication.",
                    nameof(authentication));
            }

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
                "A trunk connection does not register and must not carry a registration expiry.",
                nameof(registrationExpirySeconds));
        }

        Host = host;
        Port = port;
        Transport = transport;
        Mode = mode;
        Authentication = authentication;
        RegistrationExpirySeconds = registrationExpirySeconds;
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

    /// <summary>Requested registration expiry in seconds (≥ 1); <see langword="null"/> for a trunk.</summary>
    public int? RegistrationExpirySeconds { get; }
}
