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
/// will only succeed for local/loopback peers. The initial value comes from deployment
/// configuration; <see cref="ReportHealth"/> lets a live source move it afterwards, which is what
/// makes the WebRTC capability revocable rather than fixed for the process lifetime (#115).
/// </remarks>
public sealed class WebRtcVoiceChannel : IVoiceChannel
{
    // The channel is the WebRTC surface, so it publishes the WebRTC capability alongside voice
    // (#115). Declaring communication.webrtc in the manifest while no channel reported it left the
    // capability permanently unsatisfiable, and blocked dependent plugins whose underlying service
    // was in fact present.
    /// <summary>
    /// What this channel publishes. Exposed statically so the manifest's conditional
    /// capabilities can be checked against a real publisher without constructing an SDK client.
    /// </summary>
    public static IReadOnlyCollection<string> PublishedCapabilities { get; } =
        [CommunicationCapabilities.Voice, CommunicationCapabilities.WebRtc];

    private readonly IWebRtcClient _client;
    private readonly Lock _healthGate = new();
    private ChannelHealth _health;

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
    public IReadOnlyCollection<string> Capabilities => PublishedCapabilities;

    /// <inheritdoc />
    public ChannelHealth Health
    {
        get
        {
            lock (_healthGate)
            {
                return _health;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged;

    /// <summary>
    /// Moves the channel's health and raises <see cref="HealthChanged"/> when it actually changed.
    /// This is the seam a reachability source drives; without it the WebRTC capability could be
    /// granted at startup but never revoked when NAT traversal stops working (#115).
    /// </summary>
    public void ReportHealth(ChannelHealth health)
    {
        lock (_healthGate)
        {
            if (_health == health)
            {
                return;
            }

            _health = health;
        }

        HealthChanged?.Invoke(this, new ChannelHealthChangedEventArgs(health));
    }

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
