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
/// Health reflects external reachability: <see cref="ChannelHealth.Up"/> when at least one STUN/TURN
/// server is configured or the bind endpoint is not loopback (meaning the server is reachable from
/// outside), <see cref="ChannelHealth.Degraded"/> otherwise — the client is alive but NAT traversal
/// will only succeed for local/loopback peers. There is no dynamic health transition in v1; the value
/// is fixed at construction. <see cref="ICommunicationChannel.HealthChanged"/> exists for a future
/// source (e.g. live TURN-reachability probes).
/// </remarks>
public sealed class WebRtcVoiceChannel : IVoiceChannel
{
    private static readonly IReadOnlyCollection<string> VoiceCapability = [CommunicationCapabilities.Voice];

    private readonly IWebRtcClient _client;
    private readonly ChannelHealth _health;

    /// <summary>Wraps <paramref name="client"/> as a workspace channel identified by the given ids.</summary>
    /// <param name="channelId">Stable channel identifier.</param>
    /// <param name="displayName">Human-readable name shown in admin UIs.</param>
    /// <param name="pluginId">The owning plugin identifier.</param>
    /// <param name="client">The underlying WebRTC client; caller owns its lifetime.</param>
    /// <param name="externallyReachable">
    /// <see langword="true"/> when the deployment has STUN/TURN configured or is bound to a non-loopback
    /// address, meaning remote browsers can establish NAT-traversed connections.
    /// <see langword="false"/> for loopback-only / no-ICE-server deployments — the channel is
    /// <see cref="ChannelHealth.Degraded"/> (alive but not externally reachable).
    /// </param>
    public WebRtcVoiceChannel(
        string channelId,
        string displayName,
        string pluginId,
        IWebRtcClient client,
        bool externallyReachable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(client);

        ChannelId = channelId;
        DisplayName = displayName;
        PluginId = pluginId;
        _client = client;
        _health = externallyReachable ? ChannelHealth.Up : ChannelHealth.Degraded;
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
    public ChannelHealth Health => _health;

    /// <inheritdoc />
    // No dynamic health source in v1; the health is fixed at construction from deployment configuration.
    // The event exists for a future source (e.g. live TURN reachability probes).
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
