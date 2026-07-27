using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Domain.Accounts;
using SdkSipAccount = CalloraVoipSdk.Core.Domain.Lines.SipAccount;
using SdkSipTransport = CalloraVoipSdk.Core.Domain.Lines.SipTransport;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Maps a persisted domain <see cref="SipAccount"/> to the SDK's <see cref="SdkSipAccount"/> that the
/// voice client registers with, resolving the digest password through the plugin's own
/// <see cref="IPluginDataProtector"/> (never stored or passed in plaintext). Pure mapping — it makes
/// no network call — so it is fully unit-tested; the SDK connect that consumes its output is not.
/// </summary>
/// <remarks>
/// v1 supports digest accounts: a plain registering account and a credentialed (digest) trunk that
/// registers but accepts trunk inbound (multiple DIDs, optional outbound proxy). IP-authenticated
/// trunks (registration-less) and mutual-TLS accounts carry no SIP user identity / need certificate
/// handling designed against a real registrar (B4-deep-3), so they are rejected here rather than
/// mapped to a half-valid account.
/// </remarks>
public sealed class SdkSipAccountFactory
{
    private const int DefaultRegistrationExpirySeconds = 300;

    private readonly IPluginDataProtector _dataProtector;
    private readonly string _pluginId;

    /// <summary>Creates the factory bound to the plugin's data protector and plugin id.</summary>
    public SdkSipAccountFactory(IPluginDataProtector dataProtector, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(dataProtector);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        _dataProtector = dataProtector;
        _pluginId = pluginId;
    }

    /// <summary>
    /// Builds the SDK account for <paramref name="account"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">The account does not use digest authentication.</exception>
    /// <exception cref="InvalidOperationException">The digest password reference could not be resolved.</exception>
    public SdkSipAccount Create(SipAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var connection = account.Connection;
        if (connection.Authentication is not DigestAuthentication digest)
        {
            throw new NotSupportedException(
                $"SIP authentication method '{connection.Authentication.Method}' is not yet supported by the " +
                "SDK connector; only digest (registering) accounts can be connected.");
        }

        if (!_dataProtector.TryUnprotect(_pluginId, digest.PasswordSecretRef, out var password))
        {
            throw new InvalidOperationException(
                $"Could not resolve the SIP password for account '{account.Id}'.");
        }

        // A credentialed trunk registers but accepts trunk inbound: an optional outbound proxy and a
        // DID whitelist. For a plain register account these stay at the SDK defaults (AcceptTrunkInbound
        // already true, no proxy, no whitelist), so nothing is forced onto it. The SDK properties are
        // init-only, so the trunk fields are set here in the initializer rather than after the fact.
        var isTrunk = connection.Mode == SipAccountMode.Trunk;

        return new SdkSipAccount
        {
            DisplayName = account.DisplayName,
            Username = digest.Username,
            Password = password,
            SipServer = connection.Host,
            Port = connection.Port,
            Transport = MapTransport(connection.Transport),
            RegistrationExpiry = connection.RegistrationExpirySeconds ?? DefaultRegistrationExpirySeconds,
            AcceptTrunkInbound = true,
            OutboundProxy = isTrunk ? connection.OutboundProxy : null,
            InboundNumbers = isTrunk ? connection.InboundNumbers : null,
        };
    }

    private static SdkSipTransport MapTransport(SipTransport transport) => transport switch
    {
        SipTransport.Udp => SdkSipTransport.Udp,
        SipTransport.Tcp => SdkSipTransport.Tcp,
        SipTransport.Tls => SdkSipTransport.Tls,
        _ => throw new NotSupportedException($"Unsupported SIP transport '{transport}'."),
    };
}
