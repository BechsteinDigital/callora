using CalloraVoipSdk;
using CalloraVoipSdk.Core.Domain.Security;
using Microsoft.Extensions.Configuration;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Deployment-tunable settings for the plugin's self-built voice client, read from the
/// plugin-scoped <c>Voice</c> configuration section. Every value defaults to the SDK/reference default,
/// so an unconfigured deployment behaves exactly as before. Codec and bridge format are deliberately
/// NOT exposed: the media bridge (<c>SdkCallAudioStream</c>) is G.711 µ-law only, so they stay PCMU.
/// </summary>
internal sealed record VoiceClientOptions
{
    /// <summary>SIP signalling transport (default UDP).</summary>
    public SipTransport Transport { get; init; } = SipTransport.Udp;

    /// <summary>Media SRTP policy (default <see cref="SrtpPolicy.Optional"/>).</summary>
    public SrtpPolicy SrtpPolicy { get; init; } = SrtpPolicy.Optional;

    /// <summary>Offer DTLS-SRTP keying instead of SDES on outbound calls (default false).</summary>
    public bool OfferDtlsSrtp { get; init; }

    /// <summary>Refuse SDES keying over insecure signalling — fail-closed media security (default false).</summary>
    public bool RequireSecureSignalingForSdes { get; init; }

    /// <summary>How long to wait for inbound media before treating the call as silent (default 15 s).</summary>
    public TimeSpan InboundMediaTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Reads the options from the plugin-scoped <c>Voice</c> section; unparseable values fall back.</summary>
    public static VoiceClientOptions FromConfiguration(IConfiguration? configuration)
    {
        var defaults = new VoiceClientOptions();
        if (configuration is null)
        {
            return defaults;
        }

        var section = configuration.GetSection("Voice");
        return new VoiceClientOptions
        {
            Transport = ParseEnum(section["Transport"], defaults.Transport),
            SrtpPolicy = ParseEnum(section["SrtpPolicy"], defaults.SrtpPolicy),
            OfferDtlsSrtp = ParseBool(section["OfferDtlsSrtp"], defaults.OfferDtlsSrtp),
            RequireSecureSignalingForSdes = ParseBool(section["RequireSecureSignalingForSdes"], defaults.RequireSecureSignalingForSdes),
            InboundMediaTimeout = ParseSeconds(section["InboundMediaTimeoutSeconds"], defaults.InboundMediaTimeout),
        };
    }

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : fallback;

    private static bool ParseBool(string? raw, bool fallback) =>
        bool.TryParse(raw, out var value) ? value : fallback;

    private static TimeSpan ParseSeconds(string? raw, TimeSpan fallback) =>
        int.TryParse(raw, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : fallback;
}
