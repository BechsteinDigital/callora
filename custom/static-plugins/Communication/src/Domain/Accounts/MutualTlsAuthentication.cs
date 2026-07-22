namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// Mutual-TLS authentication: the peer is authenticated by a client certificate, referenced in the
/// secret store. No SIP username/password is involved.
/// </summary>
public sealed record MutualTlsAuthentication : SipAuthentication
{
    /// <summary>Creates and validates a mutual-TLS configuration.</summary>
    public MutualTlsAuthentication(string clientCertificateSecretRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientCertificateSecretRef);
        ClientCertificateSecretRef = clientCertificateSecretRef;
    }

    /// <inheritdoc />
    public override SipAuthMethod Method => SipAuthMethod.MutualTls;

    /// <summary>Reference to the client certificate in the secret store.</summary>
    public string ClientCertificateSecretRef { get; }
}
