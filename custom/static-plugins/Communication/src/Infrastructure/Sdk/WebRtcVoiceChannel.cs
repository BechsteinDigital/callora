using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Wraps one CalloraVoipSdk <see cref="IWebRtcClient"/> as a foundation <see cref="IVoiceChannel"/>: the
/// WebRTC counterpart to <see cref="SdkVoiceChannel"/> on the SIP path. It surfaces the voice capability
/// and channel health, and exposes a seam the signalling transport (S3) drives once a browser peer has
/// connected. Unlike SIP there is no line registration to derive health from, and no server-initiated
/// outbound placement — a WebRTC call is browser-initiated over the signalling channel.
/// </summary>
/// <remarks>
/// Health is derived from the client's liveness: the client binds no socket until a peer is created, so
/// while it is alive the channel is <see cref="ChannelHealth.Up"/> (ready to accept browser peers). There
/// is no registration handshake that could degrade, so no <see cref="ICommunicationChannel.HealthChanged"/>
/// transition is raised in v1; the event exists for a future health source (e.g. TURN reachability).
/// </remarks>
public sealed class WebRtcVoiceChannel : IVoiceChannel
{
    private static readonly IReadOnlyCollection<string> VoiceCapability = [CommunicationCapabilities.Voice];

    private readonly IWebRtcClient _client;

    /// <summary>Wraps <paramref name="client"/> as a workspace channel identified by the given ids.</summary>
    public WebRtcVoiceChannel(string channelId, string displayName, string pluginId, IWebRtcClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(client);

        ChannelId = channelId;
        DisplayName = displayName;
        PluginId = pluginId;
        _client = client;
    }

    /// <inheritdoc />
    public string ChannelId { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Capabilities => VoiceCapability;

    /// <inheritdoc />
    public ChannelHealth Health => ChannelHealth.Up;

    /// <inheritdoc />
    // No health source in v1 (no registration handshake that could degrade), so this never fires yet; it
    // stays on the contract for a future source (e.g. TURN reachability).
#pragma warning disable CS0067
    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    /// <summary>
    /// Not supported: WebRTC calls are browser-initiated via signalling; outbound placement is not
    /// supported in v1 (the call is created on the signalling path, not through this channel).
    /// </summary>
    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "WebRTC calls are browser-initiated via signalling; outbound placement is not supported in v1.");

    /// <summary>
    /// The signalling transport (S3) calls this once a browser peer has connected: it wraps the connected
    /// <paramref name="peer"/> as an inbound <see cref="WebRtcCall"/>, raises <see cref="IncomingCall"/>,
    /// and returns the call so the signalling handler can keep driving it. Kept <see langword="internal"/>
    /// so S3 can attach without this slice depending on any signalling type.
    /// </summary>
    internal ICall TrackIncomingCall(IPeerConnection peer, string callId, CallTarget target)
    {
        ArgumentNullException.ThrowIfNull(peer);

        var call = new WebRtcCall(peer, callId, CallDirection.Inbound, target);
        IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));
        return call;
    }
}
