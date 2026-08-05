using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// The authentication methods the voice provider can actually connect (#111) — one place, so
/// the admin UI, the API and the provisioner cannot disagree.
/// <para>
/// The domain models three methods because they are real SIP deployments. The provider behind
/// <see cref="IVoiceChannelConnector"/> currently connects only digest. Advertising the other
/// two anyway produced accounts that were accepted, then silently skipped at provisioning
/// while the UI sat on <c>Connecting</c> forever. This type turns that mismatch into an
/// explicit, testable boundary: unsupported methods are refused at the edge with an
/// actionable reason instead of failing later and invisibly.
/// </para>
/// <para>
/// Both gaps are tracked upstream and this type is what shrinks as they land:
/// IP-authenticated (registration-less) trunks need
/// <see href="https://github.com/BechsteinDigital/callora-voip-sdk/issues/104">SDK #104</see>,
/// mutual TLS needs per-line certificates from
/// <see href="https://github.com/BechsteinDigital/callora-voip-sdk/issues/183">SDK #183</see>
/// (the SDK's TLS configuration is client-wide and file-path based today).
/// </para>
/// </summary>
public static class SipAuthMethodSupport
{
    /// <summary>Methods the provider can connect today.</summary>
    public static IReadOnlyList<SipAuthMethod> Supported { get; } = [SipAuthMethod.Digest];

    /// <summary>Whether an account using <paramref name="method"/> can be connected.</summary>
    public static bool IsSupported(SipAuthMethod method) => method == SipAuthMethod.Digest;

    /// <summary>
    /// Operator-facing reason why <paramref name="method"/> cannot be used, or null when it can.
    /// Names the upstream gap so the message is actionable rather than a bare refusal.
    /// </summary>
    public static string? DescribeUnsupported(SipAuthMethod method) => method switch
    {
        SipAuthMethod.Digest => null,
        SipAuthMethod.IpAuthenticated =>
            "IP-authenticated trunks are not supported: the voice provider always registers and has no " +
            "registration-less mode (callora-voip-sdk#104). Use digest authentication — most trunk " +
            "providers offer a registering variant.",
        SipAuthMethod.MutualTls =>
            "Mutual-TLS accounts are not supported: the voice provider's TLS configuration is per client, " +
            "not per account, and loads its certificate from a file rather than the secret store " +
            "(callora-voip-sdk#183). Use digest authentication over a TLS transport instead.",
        _ => $"Authentication method '{method}' is not supported by the voice provider."
    };
}
