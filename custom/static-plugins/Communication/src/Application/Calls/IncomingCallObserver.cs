using Callora.Plugin.Communication.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Bridges channel-level incoming calls to the call-control primitive: for every registered channel it
/// subscribes to <see cref="ICommunicationChannel.IncomingCall"/> and hands each inbound call to
/// <see cref="CallControlService.ObserveIncomingAsync"/> (records history, publishes <c>call.ringing</c>
/// and follows the lifecycle). It never answers or routes a call — that is a consumer plugin's (e.g. a
/// PBX's) decision. Started at plugin startup; catches channels registered before and after it starts.
/// </summary>
internal sealed class IncomingCallObserver : IDisposable
{
    /// <summary>Source name every step recorded from here carries.</summary>
    private const string CommunicationPluginId = "communication";

    private readonly ICommunicationChannelRegistry _registry;
    private readonly CallControlService _callControl;
    private readonly IncomingCallOwnerRegistry? _owners;
    private readonly ICallJourney? _journey;
    private readonly ILogger _logger;

    // One IncomingCall subscription per registered channel, keyed by the channel instance so it can be
    // detached on unregister/dispose. Guarded because registry callbacks can arrive concurrently.
    private readonly Dictionary<ICommunicationChannel, EventHandler<IncomingCallEventArgs>> _subscriptions = new();
    private readonly object _gate = new();
    private bool _disposed;

    public IncomingCallObserver(
        ICommunicationChannelRegistry registry,
        CallControlService callControl,
        IncomingCallOwnerRegistry? owners = null,
        ILogger? logger = null,
        ICallJourney? journey = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _callControl = callControl ?? throw new ArgumentNullException(nameof(callControl));
        _owners = owners;
        _journey = journey;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Attaches to the registry and to every channel already registered.</summary>
    public void Start()
    {
        _registry.ChannelRegistered += OnChannelRegistered;
        _registry.ChannelUnregistered += OnChannelUnregistered;

        foreach (var (workspaceKey, channel) in _registry.GetAllRegistrations())
        {
            Subscribe(workspaceKey, channel);
        }
    }

    private void OnChannelRegistered(string workspaceKey, ICommunicationChannel channel) => Subscribe(workspaceKey, channel);

    private void OnChannelUnregistered(string workspaceKey, ICommunicationChannel channel) => Unsubscribe(channel);

    private void Subscribe(string workspaceKey, ICommunicationChannel channel)
    {
        lock (_gate)
        {
            if (_disposed || _subscriptions.ContainsKey(channel))
            {
                return;
            }

            void Handler(object? sender, IncomingCallEventArgs e) =>
                _ = HandleAsync(workspaceKey, channel, e.Call);
            _subscriptions[channel] = Handler;
            channel.IncomingCall += Handler;
        }
    }

    /// <summary>
    /// Tracks the call, then offers it to whoever signed up to decide about it.
    /// </summary>
    /// <remarks>
    /// Tracked first, offered second: history and the <c>call.ringing</c> event describe what arrived,
    /// and that is true whether or not anybody wants it. An owner that answers immediately would
    /// otherwise race the record of the call it answered.
    /// <para>
    /// With nobody signed up nothing is offered and nothing is refused — the behaviour before owners
    /// existed. Refusing here as soon as this shipped would reject every inbound call in every
    /// deployment, because no consumer registers yet. Once a workspace has an owner, a call none of
    /// them claims is rejected: at that point somebody has taken responsibility for the workspace's
    /// calls, and letting an unclaimed one ring unanswered is the worse answer.
    /// </para>
    /// </remarks>
    private async Task HandleAsync(string workspaceKey, ICommunicationChannel channel, ICall call)
    {
        await _callControl.ObserveIncomingAsync(workspaceKey, channel, call).ConfigureAwait(false);

        // The first entry in the call's story, and the only one written whether or not anybody wants
        // the call: which of our numbers it reached is exactly what a consumer decides on.
        _journey?.Record(workspaceKey, call.CallId, new CallJourneyStep(
            CommunicationPluginId,
            "call.ringing",
            $"Reached {call.InboundIdentity?.CalledNumber ?? "an unreported number"} on channel {channel.ChannelId}."));

        if (_owners is null || !_owners.HasOwners(workspaceKey))
        {
            return;
        }

        try
        {
            var owner = await _owners.OfferAsync(workspaceKey, call).ConfigureAwait(false);
            if (owner is not null)
            {
                _journey?.Record(workspaceKey, call.CallId, new CallJourneyStep(
                    CommunicationPluginId, "call.claimed", $"Taken by {owner.DisplayName}."));
                return;
            }

            // "Rejected" on its own reads like a fault. That nobody claimed it is the actual reason,
            // and the one an operator can act on — usually by assigning the number.
            _journey?.Record(workspaceKey, call.CallId, new CallJourneyStep(
                CommunicationPluginId,
                "call.unclaimed",
                "No consumer of this workspace answers this number; the call was rejected."));

            await call.RejectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Rejecting can fail on its own (the caller hung up meanwhile). Logged rather than
            // propagated: this runs fire-and-forget off the channel's event dispatch.
            _logger.LogWarning(ex,
                "Offering inbound call {CallId} to its owners failed.", call.CallId);
        }
    }

    private void Unsubscribe(ICommunicationChannel channel)
    {
        lock (_gate)
        {
            if (_subscriptions.Remove(channel, out var handler))
            {
                channel.IncomingCall -= handler;
            }
        }
    }

    /// <summary>Detaches from the registry and every channel so nothing dangles past plugin stop.</summary>
    public void Dispose()
    {
        _registry.ChannelRegistered -= OnChannelRegistered;
        _registry.ChannelUnregistered -= OnChannelUnregistered;

        lock (_gate)
        {
            _disposed = true;
            foreach (var (channel, handler) in _subscriptions)
            {
                channel.IncomingCall -= handler;
            }

            _subscriptions.Clear();
        }
    }
}
