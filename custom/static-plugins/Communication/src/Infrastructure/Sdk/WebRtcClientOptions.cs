using System.Net;
using CalloraVoipSdk;
using Microsoft.Extensions.Configuration;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Deployment-tunable settings for the plugin's self-built WebRTC client, read from the
/// <c>Communication:WebRtc</c> configuration section. Mirrors <see cref="VoiceClientOptions"/> for the
/// SIP facade: every value defaults to the SDK/WebRTC standard, so an unconfigured deployment offers
/// Opus over a fresh per-peer DTLS identity with host-only ICE gathering.
/// </summary>
/// <remarks>
/// v1 is audio-focused. Opus is the WebRTC audio standard, so it is the codec default and the only one
/// a browser peer needs. Unlike the SIP path there is no µ-law media bridge here — WebRTC is Opus /
/// transport-only, and any WebRTC↔SIP transcoding is a separate, deferred slice. <see cref="EnableVideo"/>
/// is carried through to the SDK for a future conferencing consumer but is not wired to any media path.
/// </remarks>
internal sealed record WebRtcClientOptions
{
    /// <summary>ICE helper servers (STUN/TURN) for server-reflexive/relay candidate gathering. Default: none (host-only).</summary>
    public IReadOnlyList<IceServerConfiguration> IceServers { get; init; } = [];

    /// <summary>Audio codecs to offer, by name. Default is Opus — the WebRTC audio standard.</summary>
    public IReadOnlyList<string> AudioCodecs { get; init; } = ["opus"];

    /// <summary>Whether to offer a video m-line. Default false; v1 passes this through without wiring media.</summary>
    public bool EnableVideo { get; init; }

    /// <summary>Local media endpoint the peer binds. Default is an ephemeral loopback port (SDK default).</summary>
    public IPEndPoint LocalEndPoint { get; init; } = new(IPAddress.Loopback, 0);

    /// <summary>
    /// Reads the options from the <c>Communication:WebRtc</c> section; unparseable values fall back to the
    /// defaults. ICE servers are read from the <c>IceServers</c> array (each entry: Host, optional Port,
    /// Type stun|turn, Transport udp|tcp|tls, optional Username/Password).
    /// </summary>
    public static WebRtcClientOptions FromConfiguration(IConfiguration? configuration)
    {
        var defaults = new WebRtcClientOptions();
        if (configuration is null)
        {
            return defaults;
        }

        var section = configuration.GetSection("Communication:WebRtc");
        return new WebRtcClientOptions
        {
            IceServers = ReadIceServers(section.GetSection("IceServers")),
            AudioCodecs = ReadCodecs(section.GetSection("AudioCodecs"), defaults.AudioCodecs),
            EnableVideo = ParseBool(section["EnableVideo"], defaults.EnableVideo),
            LocalEndPoint = ParseEndPoint(section["LocalEndPoint"], defaults.LocalEndPoint),
        };
    }

    private static IReadOnlyList<IceServerConfiguration> ReadIceServers(IConfigurationSection section)
    {
        var servers = new List<IceServerConfiguration>();
        foreach (var entry in section.GetChildren())
        {
            var host = entry["Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                continue; // A server entry without a host is meaningless — skip it rather than fail the load.
            }

            servers.Add(new IceServerConfiguration
            {
                Host = host,
                Port = int.TryParse(entry["Port"], out var port) ? port : null,
                Type = ParseEnum(entry["Type"], IceServerType.Stun),
                Transport = ParseEnum(entry["Transport"], IceTransport.Udp),
                Username = entry["Username"],
                Password = entry["Password"],
            });
        }

        return servers;
    }

    private static IReadOnlyList<string> ReadCodecs(IConfigurationSection section, IReadOnlyList<string> fallback)
    {
        var codecs = section.GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        return codecs.Length > 0 ? codecs : fallback;
    }

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : fallback;

    private static bool ParseBool(string? raw, bool fallback) =>
        bool.TryParse(raw, out var value) ? value : fallback;

    private static IPEndPoint ParseEndPoint(string? raw, IPEndPoint fallback) =>
        IPEndPoint.TryParse(raw ?? string.Empty, out var value) ? value : fallback;
}
