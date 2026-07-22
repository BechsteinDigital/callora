namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>How a <see cref="SipConnection"/> authenticates against the registrar or trunk.</summary>
public enum SipAuthMethod
{
    /// <summary>SIP digest authentication with a username and a password reference.</summary>
    Digest,

    /// <summary>IP-based authentication — the trunk trusts the source address; no credentials.</summary>
    IpAuthenticated,

    /// <summary>Mutual TLS — the peer is authenticated by a client certificate.</summary>
    MutualTls
}
