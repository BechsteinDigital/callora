using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Channels;

/// <summary>
/// Publishes the plugin's conference surface as a workspace channel so
/// <see cref="CommunicationCapabilities.Video"/> becomes satisfiable (#115).
/// <para>
/// The SFU behind <c>IConferenceService</c> was exported as a service only. Runtime capabilities
/// are derived from registered channels, so nothing ever reported video and the manifest's
/// conditional <c>communication.video</c> could never be granted. A dependent plugin was then
/// blocked from activating even though the service it needs was running.
/// </para>
/// <para>
/// A conference is joined, not dialled, so this channel declines
/// <see cref="PlaceCallAsync"/>. Call routing only ever selects channels by the voice capability,
/// which this one does not publish, so that path is never reached in practice.
/// </para>
/// </summary>
public sealed class ConferenceChannel : ICommunicationChannel
{
    /// <summary>
    /// What this channel publishes. Exposed statically so the manifest's conditional
    /// capabilities can be checked against a real publisher.
    /// </summary>
    public static IReadOnlyCollection<string> PublishedCapabilities { get; } =
        [CommunicationCapabilities.Video];

    private readonly Lock _healthGate = new();
    private ChannelHealth _health;

    /// <summary>Creates the conference channel for one workspace.</summary>
    /// <param name="channelId">Stable channel identifier.</param>
    /// <param name="displayName">Human-readable name shown in admin UIs.</param>
    /// <param name="pluginId">The owning plugin identifier.</param>
    /// <param name="externallyReachable">
    /// Whether remote browsers can reach the SFU (STUN/TURN configured, or a non-loopback bind).
    /// A conference rides the same NAT traversal as any other WebRTC session, so an unreachable
    /// deployment is <see cref="ChannelHealth.Degraded"/>: usable locally, not for real participants.
    /// </param>
    public ConferenceChannel(string channelId, string displayName, string pluginId, bool externallyReachable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        ChannelId = channelId;
        DisplayName = displayName;
        PluginId = pluginId;
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

    /// <inheritdoc />
    // A conference has no inbound call notion: participants join an existing conference.
#pragma warning disable CS0067
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;
#pragma warning restore CS0067

    /// <summary>
    /// Moves the channel's health and raises <see cref="HealthChanged"/> when it changed. The seam
    /// that makes the video capability revocable when the SFU stops being usable.
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
    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "A conference channel is joined through IConferenceService, not dialled.");
}
