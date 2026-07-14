using System.Threading.Channels;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugins.Voip.Application.Calls;

/// <summary>
/// Workspace-scoped subscription on the hub's event stream. Events are
/// buffered in a bounded channel; slow consumers lose the oldest events
/// instead of blocking publishers.
/// </summary>
public sealed class VoipCallEventSubscription : ICallEventSubscription
{
    private readonly Channel<CallStreamEvent> _channel = Channel.CreateBounded<CallStreamEvent>(
        new BoundedChannelOptions(capacity: 64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private readonly Action<Guid> _onDispose;

    internal VoipCallEventSubscription(Guid id, string workspaceKey, Action<Guid> onDispose)
    {
        Id = id;
        WorkspaceKey = workspaceKey;
        _onDispose = onDispose;
    }

    public Guid Id { get; }

    public string WorkspaceKey { get; }

    public ChannelReader<CallStreamEvent> Reader => _channel.Reader;

    internal void Write(CallStreamEvent callEvent) => _channel.Writer.TryWrite(callEvent);

    internal void Complete() => _channel.Writer.TryComplete();

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _onDispose(Id);
    }
}
