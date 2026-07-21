namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// The connection configuration of a <see cref="SipAccount"/> — grouped as one value object
/// (Introduce Parameter Object) because host/port/transport/auth form one cohesive clump.
/// The password is never held here; only a reference into the secret store.
/// </summary>
public sealed record SipConnection
{
    /// <summary>Creates and validates a connection configuration.</summary>
    public SipConnection(
        string host,
        int port,
        SipTransport transport,
        SipAccountMode mode,
        string authUsername,
        string? authId,
        string passwordSecretRef,
        int registrationExpirySeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentException.ThrowIfNullOrWhiteSpace(authUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordSecretRef);
        ArgumentOutOfRangeException.ThrowIfLessThan(registrationExpirySeconds, 1);

        Host = host;
        Port = port;
        Transport = transport;
        Mode = mode;
        AuthUsername = authUsername;
        AuthId = authId;
        PasswordSecretRef = passwordSecretRef;
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

    /// <summary>Authentication user name.</summary>
    public string AuthUsername { get; }

    /// <summary>Optional distinct authentication id (defaults to the user name when null).</summary>
    public string? AuthId { get; }

    /// <summary>Reference to the password in the secret store.</summary>
    public string PasswordSecretRef { get; }

    /// <summary>Requested registration expiry in seconds (≥ 1).</summary>
    public int RegistrationExpirySeconds { get; }
}
