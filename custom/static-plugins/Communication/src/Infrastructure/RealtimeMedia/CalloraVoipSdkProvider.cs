using Callora.Plugin.Communication.Application.RealtimeMedia;
using CalloraVoipSdk;
using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// The first <see cref="IRealtimeMediaProvider"/> adapter: binds the CalloraVoipSdk. It holds one SDK
/// <see cref="IWebRtcClient"/> and mints an <see cref="IMediaPeer"/> per neutral <see cref="MediaPeerOptions"/>
/// by creating an SDK <see cref="IPeerConnection"/> and wrapping it in a <see cref="CalloraVoipSdkMediaPeer"/>.
/// This is the only place in communication's media stack that names <c>CalloraVoipSdk</c>; the layer above
/// the port sees no SDK type. It consolidates the existing <c>HeadlessWebRtcClientFactory</c> mapping.
/// </summary>
/// <remarks>
/// The SDK's <see cref="WebRtcConfiguration"/> is per-client (ICE servers, codecs, endpoint), while the port
/// takes options per peer. This adapter builds one client from the first peer's options; a later slice can
/// key clients by configuration if a deployment needs divergent per-peer ICE. The caller owns the provider
/// and disposes it (which disposes the underlying client).
/// </remarks>
internal sealed class CalloraVoipSdkProvider : IRealtimeMediaProvider, IAsyncDisposable
{
    private readonly IWebRtcClient _client;

    /// <summary>Creates the provider over an existing SDK client (the caller owns client construction).</summary>
    public CalloraVoipSdkProvider(IWebRtcClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>Builds a provider by constructing an SDK client from the given options.</summary>
    public static CalloraVoipSdkProvider Create(MediaPeerOptions options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var client = new WebRtcClient(BuildConfiguration(options, loggerFactory));
        return new CalloraVoipSdkProvider(client);
    }

    /// <inheritdoc />
    public IMediaPeer CreatePeer(MediaPeerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var peer = _client.CreatePeer();
        return new CalloraVoipSdkMediaPeer(peer);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _client.DisposeAsync();

    /// <summary>
    /// Maps the neutral <see cref="MediaPeerOptions"/> onto the SDK's immutable
    /// <see cref="WebRtcConfiguration"/> — a pure function so the mapping is verifiable without a real client.
    /// </summary>
    internal static WebRtcConfiguration BuildConfiguration(MediaPeerOptions options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new WebRtcConfiguration
        {
            IceServers = [.. options.IceServers.Select(MapIceServer)],
            AudioCodecs = options.AudioCodecs,
            VideoCodecs = options.VideoCodecs,
            EnableVideo = options.EnableVideo,
            UseStableNumericMediaIds = options.UseStableNumericMediaIds,
            LocalEndPoint = options.LocalEndPoint,
            LoggerFactory = loggerFactory,
        };
    }

    private static IceServerConfiguration MapIceServer(MediaIceServer server) => new()
    {
        Host = server.Host,
        Port = server.Port,
        Type = ParseEnum(server.Kind, IceServerType.Stun),
        Transport = ParseEnum(server.Transport, IceTransport.Udp),
        Username = server.Username,
        Password = server.Password,
    };

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : fallback;
}
