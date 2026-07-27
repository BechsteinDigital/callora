using Callora.Plugin.Communication.Abstractions;

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
    private readonly ICommunicationChannelRegistry _registry;
    private readonly CallControlService _callControl;

    // One IncomingCall subscription per registered channel, keyed by the channel instance so it can be
    // detached on unregister/dispose. Guarded because registry callbacks can arrive concurrently.
    private readonly Dictionary<ICommunicationChannel, EventHandler<IncomingCallEventArgs>> _subscriptions = new();
    private readonly object _gate = new();
    private bool _disposed;

    public IncomingCallObserver(ICommunicationChannelRegistry registry, CallControlService callControl)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _callControl = callControl ?? throw new ArgumentNullException(nameof(callControl));
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
                _ = _callControl.ObserveIncomingAsync(workspaceKey, channel, e.Call);
            _subscriptions[channel] = Handler;
            channel.IncomingCall += Handler;
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
