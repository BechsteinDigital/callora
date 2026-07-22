namespace Callora.Plugin.Communication.Infrastructure.Channels;

/// <summary>
/// The disposable handle returned by <see cref="CommunicationChannelRegistry.Register"/>: runs its
/// removal action exactly once, so disposing twice (or after the entry is already gone) is a no-op.
/// </summary>
internal sealed class ChannelRegistrationHandle(Action removeAction) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            removeAction();
        }
    }
}
