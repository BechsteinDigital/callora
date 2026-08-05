using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Channels;
using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Provisions workspace-scoped <see cref="WebRtcVoiceChannel"/>s on demand, mirroring the
/// lifecycle of <see cref="VoiceChannelProvisioner"/> on the SIP path. One shared
/// <see cref="IWebRtcClient"/> is reused across all workspaces in v1 (one client, channels
/// workspace-isolated); multi-client routing is deferred. The plugin owns the client's
/// lifecycle; this provisioner neither creates nor disposes it.
/// </summary>
internal sealed class WebRtcChannelProvisioner
{
    private readonly IWebRtcClient _client;
    private readonly ICommunicationChannelRegistry _registry;
    private readonly string _pluginId;
    private readonly bool _externallyReachable;
    private readonly ILogger<WebRtcChannelProvisioner> _logger;

    // Protects _channels and _registrations from concurrent get-or-create/teardown calls.
    private readonly object _gate = new();
    private readonly Dictionary<string, WebRtcVoiceChannel> _channels =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConferenceChannel> _conferenceChannels =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDisposable> _registrations = [];

    /// <param name="client">The shared WebRTC client; caller (plugin) owns and disposes it.</param>
    /// <param name="registry">The host channel registry to register each new channel into.</param>
    /// <param name="pluginId">The plugin identifier stamped onto every provisioned channel.</param>
    /// <param name="externallyReachable">
    /// Forwarded to each <see cref="WebRtcVoiceChannel"/>: <see langword="true"/> when the deployment
    /// has STUN/TURN configured or binds a non-loopback address. Channels created with
    /// <see langword="false"/> report <see cref="Abstractions.ChannelHealth.Degraded"/>.
    /// </param>
    /// <param name="logger">Diagnostic logger.</param>
    public WebRtcChannelProvisioner(
        IWebRtcClient client,
        ICommunicationChannelRegistry registry,
        string pluginId,
        bool externallyReachable,
        ILogger<WebRtcChannelProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _registry = registry;
        _pluginId = pluginId;
        _externallyReachable = externallyReachable;
        _logger = logger;
    }

    /// <summary>The shared WebRTC client — the signalling path uses it to create server-side peers.</summary>
    public IWebRtcClient Client => _client;

    /// <summary>
    /// The conference channel of a workspace, once provisioned. Exposed so a reachability source
    /// can move its health and thereby grant or revoke the video capability (#115).
    /// </summary>
    public ConferenceChannel? GetConferenceChannel(string workspaceKey)
    {
        lock (_gate)
        {
            return _conferenceChannels.GetValueOrDefault(workspaceKey);
        }
    }

    /// <summary>
    /// Returns the <see cref="WebRtcVoiceChannel"/> for <paramref name="workspaceKey"/>, creating and
    /// registering one if none exists yet. Idempotent: subsequent calls with the same key return the
    /// same instance.
    /// </summary>
    public WebRtcVoiceChannel GetOrCreateChannel(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        lock (_gate)
        {
            if (_channels.TryGetValue(workspaceKey, out var existing))
            {
                return existing;
            }

            var channelId = $"webrtc-{workspaceKey}";
            var channel = new WebRtcVoiceChannel(channelId, "WebRTC", _pluginId, _client, _externallyReachable);
            _registrations.Add(_registry.Register(workspaceKey, channel));
            _channels[workspaceKey] = channel;

            // The SFU rides the same client and the same NAT traversal, so the conference
            // surface exists exactly where the WebRTC one does. Registering it here is what
            // makes communication.video satisfiable at all (#115): capabilities are derived
            // from channels, and exporting IConferenceService as a service published nothing.
            var conferenceChannelId = $"conference-{workspaceKey}";
            var conferenceChannel = new ConferenceChannel(
                conferenceChannelId, "Conference", _pluginId, _externallyReachable);
            _registrations.Add(_registry.Register(workspaceKey, conferenceChannel));
            _conferenceChannels[workspaceKey] = conferenceChannel;

            _logger.LogInformation(
                "WebRTC channel '{ChannelId}' and conference channel '{ConferenceChannelId}' provisioned for workspace '{WorkspaceKey}'.",
                channelId,
                conferenceChannelId,
                workspaceKey);

            return channel;
        }
    }

    /// <summary>
    /// Deregisters every channel this provisioner created (called during plugin stop).
    /// Does NOT dispose the client — the plugin owns it.
    /// </summary>
    public void Teardown()
    {
        lock (_gate)
        {
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
            _channels.Clear();
            _conferenceChannels.Clear();
        }

        _logger.LogInformation("WebRTC channel provisioner torn down.");
    }
}
