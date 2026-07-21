namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>Transport used for the SIP signalling of an account.</summary>
public enum SipTransport
{
    /// <summary>UDP transport.</summary>
    Udp = 0,

    /// <summary>TCP transport.</summary>
    Tcp = 1,

    /// <summary>TLS (SIPS) transport.</summary>
    Tls = 2
}
