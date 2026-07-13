using System.Threading.Channels;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// One workspace-scoped subscription on the call event stream. Events are
/// buffered in a bounded channel; slow consumers lose the oldest events
/// instead of blocking publishers.
/// </summary>
public sealed class CallEventSubscription : IDisposable
{
    private readonly Channel<CallEvent> _channel = Channel.CreateBounded<CallEvent>(
        new BoundedChannelOptions(capacity: 64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private readonly Action<Guid> _onDispose;

    internal CallEventSubscription(Guid id, string workspaceKey, Action<Guid> onDispose)
    {
        Id = id;
        WorkspaceKey = workspaceKey;
        _onDispose = onDispose;
    }

    public Guid Id { get; }

    public string WorkspaceKey { get; }

    public ChannelReader<CallEvent> Reader => _channel.Reader;

    internal void Write(CallEvent callEvent) => _channel.Writer.TryWrite(callEvent);

    /// <summary>
    /// Completes the channel so the consuming stream ends gracefully
    /// (host shutdown, PLAT-234).
    /// </summary>
    internal void Complete() => _channel.Writer.TryComplete();

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _onDispose(Id);
    }
}
